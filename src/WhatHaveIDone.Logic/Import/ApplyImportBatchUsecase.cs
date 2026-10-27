using System.Security.Cryptography;
using System.Text;
using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Tags;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed record ApplyImportBatchUsecase(ImportRun Next, List<ImportedProject> Projects, List<ImportedEntry> Entries) : IRequest<bool>;

public sealed class ApplyImportBatchUsecaseHandler(IRepository<ImportRun> imports,IRepository<UserProfile> profiles,IRepository<Project> projects,IRepository<TimeEntry> entries,
    IReadOnlyRepository<ImportExclusion> exclusions,TagCatalog tags,TimeProvider clock) : UsecaseHandlerBase<ApplyImportBatchUsecase,bool>
{
    private ImportRun original=null!;
    protected override async Task ValidateAccessAsync(ApplyImportBatchUsecase request,CancellationToken cancellationToken)
    {
        // Internal background operation: the worker supplies the persisted job, never an HTTP owner parameter.
        original=await imports.RequireAsync(request.Next.OwnerId,request.Next.Id,cancellationToken);
        OwnedDocuments.CheckVersion(original,request.Next.Version);
        if(original.Status is not ("Queued" or "Running" or "Waiting") || (!original.Confirmed && (request.Entries.Count>0||request.Projects.Count>0)))throw new AppException("Conflict",409);
    }
    protected override async Task<bool> ExecuteAsync(ApplyImportBatchUsecase request,CancellationToken cancellationToken)
    {
        var next=request.Next;var owner=original.OwnerId;var profile=await profiles.RequireAsync(owner,owner,cancellationToken);
        Dictionary<string,long> originalTagIds = [];
        foreach (var tag in next.TagNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            originalTagIds.TryAdd(Tag.Normalize(tag.Value), tag.Key);
        }
        foreach(var item in request.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key="toggl:project:"+item.OriginalId;var id=SourceId(owner,key);
            var existing=await projects.GetBySpecAsync(new OwnedByIdSpecification<Project>(owner,id),cancellationToken);
            if(existing is not null)continue;
            await projects.AddAsync(new Project{Id=id,OwnerId=owner,ClientId=profile.DefaultClientId,CompatibilityId=item.OriginalId,Name=item.Name,Color=System.Text.RegularExpressions.Regex.IsMatch(item.Color,"^#[0-9a-fA-F]{6}$")?item.Color:"#009cde",Archived=item.Archived,SourceKey=key},cancellationToken);
        }
        foreach(var item in request.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if(original.Kind=="Api"&&item.SourceUserId!=original.SourceUserId || original.Kind=="Csv"&&!string.Equals(item.SourceEmail,original.SourceEmail,StringComparison.OrdinalIgnoreCase)){next.Skipped++;continue;}
            if(item.ErrorCode is not null){next.Errors++;next.MessageCode=item.ErrorCode;continue;}
            var id=SourceId(owner,item.SourceKey);
            if(await exclusions.GetBySpecAsync(new OwnedByIdSpecification<ImportExclusion>(owner,id),cancellationToken)is not null || await entries.GetBySpecAsync(new OwnedByIdSpecification<TimeEntry>(owner,id),cancellationToken)is not null){next.Skipped++;continue;}
            if(item.OriginalId.HasValue && await entries.GetBySpecAsync(new EntryCompatibilityIdSpecification(owner,item.OriginalId.Value),cancellationToken)is not null)throw new AppException("ImportIdCollision",409);
            string? projectId=null;
            if(item.ProjectId.HasValue)
            {
                projectId=SourceId(owner,"toggl:project:"+item.ProjectId.Value);
                if(await projects.GetBySpecAsync(new OwnedByIdSpecification<Project>(owner,projectId),cancellationToken)is null)
                {
                    if(string.IsNullOrWhiteSpace(item.ProjectName)){next.Errors++;next.MessageCode="ImportSource";continue;}
                    await projects.AddAsync(new Project{Id=projectId,OwnerId=owner,ClientId=profile.DefaultClientId,CompatibilityId=item.ProjectId.Value,Name=item.ProjectName,SourceKey="toggl:project:"+item.ProjectId.Value},cancellationToken);
                }
            }
            else if(!string.IsNullOrWhiteSpace(item.ProjectName))
            {
                var source="file-project:"+original.FileHash+":"+item.ProjectName;projectId=SourceId(owner,source);
                if(await projects.GetBySpecAsync(new OwnedByIdSpecification<Project>(owner,projectId),cancellationToken)is null)
                    await projects.AddAsync(new Project{Id=projectId,OwnerId=owner,ClientId=profile.DefaultClientId,CompatibilityId=profile.AllocateId(),Name=item.ProjectName,SourceKey=source},cancellationToken);
            }
            if(item.End is null&&profile.RunningEntryId is not null){next.Errors++;next.MessageCode="Conflict";continue;}
            if(item.End.HasValue&&item.End<item.Start){next.Errors++;next.MessageCode="InvalidInterval";continue;}
            var entry=new TimeEntry{Id=id,OwnerId=owner,CompatibilityId=item.OriginalId??profile.AllocateId(),SourceKey=item.SourceKey};
            entry.Change(item.Description,projectId,await tags.ResolveAsync(profile,item.Tags,cancellationToken,originalTagIds),item.Start,item.End);
            await entries.AddAsync(entry,cancellationToken);if(item.End is null)profile.RunningEntryId=id;next.Imported++;
        }
        next.UpdatedAt=clock.GetUtcNow();
        if(next.Status is "Completed" or "CompletedWithWarnings")
        {next.Status=next.Errors>0?"CompletedWithWarnings":"Completed";next.ProtectedToken=null;next.FilePath=null;next.TagNames.Clear();if(profile.ActiveImportRunId==next.Id)profile.ActiveImportRunId=null;}
        if(request.Entries.Count>0||next.Status is "Completed" or "CompletedWithWarnings")await profiles.UpdateAsync(profile,cancellationToken);
        await imports.UpdateAsync(next,cancellationToken);return true;
    }
    public static string SourceId(string owner,string source)=>owner+":"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
}
