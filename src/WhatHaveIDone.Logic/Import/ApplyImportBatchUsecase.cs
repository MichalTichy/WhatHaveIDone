using WhatHaveIDone.API.Models;
using System.Security.Cryptography;
using System.Text;
using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Logic.Tags;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed record ApplyImportBatchUsecase(string Id, Guid Lease, List<ImportedEntry> Entries,
    ImportCall? Continuation = null) : IRequest<bool>;

public sealed class ApplyImportBatchUsecaseHandler(IRepository<ImportRun> imports, IRepository<UserProfile> profiles,IRepository<Project> projects,IRepository<TimeEntry> entries,
    TagCatalog tags,TimeProvider clock) : UsecaseHandlerBase<ApplyImportBatchUsecase,bool>
{
    protected override async Task<bool> ExecuteAsync(ApplyImportBatchUsecase request,CancellationToken cancellationToken)
    {
        var next = await imports.RequireAsync(request.Id, cancellationToken);
        next.RequireLease(request.Lease);
        var profile = await profiles.RequireAsync(new UserProfileSpecification(), cancellationToken);
        var owner = profile.Id;

        var index = next.Calls.FindIndex(call => call.Status == ImportCallStatus.Todo);
        if (index < 0) throw new AppException("Conflict", 409);
        next.Calls[index] = next.Calls[index] with { Status = ImportCallStatus.Done };
        // Give each workspace its newest page before proceeding to older pages.
        if (request.Continuation is { } continuation) next.Calls.Add(continuation);
        var complete = next.Calls.All(call => call.Status == ImportCallStatus.Done);

        Dictionary<string,long> originalTagIds = [];
        foreach (var tag in next.TagNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            originalTagIds.TryAdd(Tag.Normalize(tag.Value), tag.Key);
        }
        foreach(var item in request.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.SourceUserId != next.SourceUserId) { next.Skipped++; continue; }
            if(item.ErrorCode is not null){next.Errors++;next.MessageCode=item.ErrorCode;continue;}
            var sourceKey = "toggl:entry:" + item.OriginalId;
            var id = SourceId(owner, sourceKey);
            if(await entries.GetByIdAsync(id, cancellationToken)is not null){next.Skipped++;continue;}
            if(await entries.GetBySpecAsync(new EntryCompatibilityIdSpecification(item.OriginalId),cancellationToken)is not null)throw new AppException("ImportIdCollision",409);
            string? projectId=null;
            if(item.ProjectId.HasValue)
            {
                projectId=SourceId(owner,"toggl:project:"+item.ProjectId.Value);
                if(await projects.GetByIdAsync(projectId, cancellationToken)is null)
                {
                    var metadata = next.SourceProjects.GetValueOrDefault(item.ProjectId.Value);
                    var name = metadata?.Name ?? item.ProjectName;
                    if (string.IsNullOrWhiteSpace(name)) { next.Errors++; next.MessageCode = "ImportSource"; continue; }
                    var project = new Project
                    {
                        Id = projectId, ClientId = profile.DefaultClientId, CompatibilityId = item.ProjectId.Value,
                        Name = name, SourceKey = "toggl:project:" + item.ProjectId.Value, Archived = metadata?.Archived ?? false
                    };
                    if (metadata is not null && System.Text.RegularExpressions.Regex.IsMatch(metadata.Color, "^#[0-9a-fA-F]{6}$"))
                        project.Color = metadata.Color;
                    await projects.AddAsync(project, cancellationToken);
                }
            }
            if(item.End is null&&profile.RunningEntryId is not null){next.Errors++;next.MessageCode="Conflict";continue;}
            if(item.End.HasValue&&item.End<item.Start){next.Errors++;next.MessageCode="InvalidInterval";continue;}
            var entry=new TimeEntry{Id=id,CompatibilityId=item.OriginalId,SourceKey=sourceKey};
            entry.Change(item.Description,projectId,await tags.ResolveAsync(profile,item.Tags,cancellationToken,originalTagIds),item.Start,item.End);
            await entries.AddAsync(entry, cancellationToken);
            if (item.End is null) profile.RunningEntryId = id;
            next.Imported++;
            if (next.RangeStart is null || item.Start < next.RangeStart) next.RangeStart = item.Start;
            if (next.RangeEnd is null || item.Start > next.RangeEnd) next.RangeEnd = item.Start;
        }
        next.UpdatedAt = clock.GetUtcNow();
        next.RetryCount = 0;
        next.NextAttemptAt = null;
        next.Release();
        next.Status = ImportStatus.Running;
        if (complete)
        {
            next.Status = next.Errors > 0 ? ImportStatus.CompletedWithWarnings : ImportStatus.Completed;
            next.Finish();
        }
        await imports.UpdateAsync(next, cancellationToken);
        await profiles.UpdateAsync(profile, cancellationToken);
        return complete;
    }

    public static string SourceId(string owner, string source)
        => owner + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
}
