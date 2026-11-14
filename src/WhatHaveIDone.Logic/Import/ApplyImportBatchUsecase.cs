using System.Security.Cryptography;
using System.Text;
using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Logic.Tags;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed record ApplyImportBatchUsecase(ImportRun Next, List<ImportedProject> Projects, List<ImportedEntry> Entries) : IRequest<bool>;

public sealed class ApplyImportBatchUsecaseHandler(IRepository<UserProfile> profiles,IRepository<Project> projects,IRepository<TimeEntry> entries,
    TagCatalog tags,TimeProvider clock) : UsecaseHandlerBase<ApplyImportBatchUsecase,bool>
{
    protected override async Task<bool> ExecuteAsync(ApplyImportBatchUsecase request,CancellationToken cancellationToken)
    {
        // Internal background operation: the worker supplies the persisted job, never an HTTP owner parameter.
        var profile=await profiles.RequireAsync(new UserProfileSpecification(),cancellationToken);
        var original=profile.RequireImport(request.Next.Id);
        if(original.Version!=request.Next.Version)throw new AppException("Conflict",409);
        if(original.Status is not ("Queued" or "Running" or "Waiting") || (!original.Confirmed && (request.Entries.Count>0||request.Projects.Count>0)))throw new AppException("Conflict",409);
        var next=request.Next;var owner=profile.Id;
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
            var existing=await projects.GetByIdAsync(id, cancellationToken);
            if(existing is not null)continue;
            await projects.AddAsync(new Project{Id=id,ClientId=profile.DefaultClientId,CompatibilityId=item.OriginalId,Name=item.Name,Color=System.Text.RegularExpressions.Regex.IsMatch(item.Color,"^#[0-9a-fA-F]{6}$")?item.Color:"#009cde",Archived=item.Archived,SourceKey=key},cancellationToken);
        }
        foreach(var item in request.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if(original.Kind=="Api"&&item.SourceUserId!=original.SourceUserId || original.Kind=="Csv"&&!string.Equals(item.SourceEmail,original.SourceEmail,StringComparison.OrdinalIgnoreCase)){next.Skipped++;continue;}
            if(item.ErrorCode is not null){next.Errors++;next.MessageCode=item.ErrorCode;continue;}
            var id=SourceId(owner,item.SourceKey);
            if(await entries.GetByIdAsync(id, cancellationToken)is not null){next.Skipped++;continue;}
            if(item.OriginalId.HasValue && await entries.GetBySpecAsync(new EntryCompatibilityIdSpecification(item.OriginalId.Value),cancellationToken)is not null)throw new AppException("ImportIdCollision",409);
            string? projectId=null;
            if(item.ProjectId.HasValue)
            {
                projectId=SourceId(owner,"toggl:project:"+item.ProjectId.Value);
                if(await projects.GetByIdAsync(projectId, cancellationToken)is null)
                {
                    if(string.IsNullOrWhiteSpace(item.ProjectName)){next.Errors++;next.MessageCode="ImportSource";continue;}
                    await projects.AddAsync(new Project{Id=projectId,ClientId=profile.DefaultClientId,CompatibilityId=item.ProjectId.Value,Name=item.ProjectName,SourceKey="toggl:project:"+item.ProjectId.Value},cancellationToken);
                }
            }
            else if(!string.IsNullOrWhiteSpace(item.ProjectName))
            {
                var source="file-project:"+original.FileHash+":"+item.ProjectName;projectId=SourceId(owner,source);
                if(await projects.GetByIdAsync(projectId, cancellationToken)is null)
                    await projects.AddAsync(new Project{Id=projectId,ClientId=profile.DefaultClientId,CompatibilityId=profile.AllocateId(),Name=item.ProjectName,SourceKey=source},cancellationToken);
            }
            if(item.End is null&&profile.RunningEntryId is not null){next.Errors++;next.MessageCode="Conflict";continue;}
            if(item.End.HasValue&&item.End<item.Start){next.Errors++;next.MessageCode="InvalidInterval";continue;}
            var entry=new TimeEntry{Id=id,CompatibilityId=item.OriginalId??profile.AllocateId(),SourceKey=item.SourceKey};
            entry.Change(item.Description,projectId,await tags.ResolveAsync(profile,item.Tags,cancellationToken,originalTagIds),item.Start,item.End);
            await entries.AddAsync(entry,cancellationToken);if(item.End is null)profile.RunningEntryId=id;next.Imported++;
        }
        next.UpdatedAt=clock.GetUtcNow();
        if(next.Status is "Completed" or "CompletedWithWarnings")
        {next.Status=next.Errors>0?"CompletedWithWarnings":"Completed";next.Finish();}
        next.Version=Guid.NewGuid();profile.Import=next;
        await profiles.UpdateAsync(profile,cancellationToken);return true;
    }
    public static string SourceId(string owner,string source)=>owner+":"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
}
