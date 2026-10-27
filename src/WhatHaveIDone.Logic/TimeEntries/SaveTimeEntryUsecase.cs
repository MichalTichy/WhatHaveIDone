using MediatR;
using Shared.Infrastructure.CurrentUserProvider;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Tags;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.TimeEntries;

public sealed record SaveTimeEntryUsecase(string? Id, EditTimeEntry Input) : IRequest<string>;

public sealed class SaveTimeEntryUsecaseHandler(ICurrentUserIdProvider actor, IRepository<UserProfile> profiles, IRepository<TimeEntry> entries,
    IReadOnlyRepository<Project> projects, TagCatalog tags, TimeProvider clock) : OwnerUsecaseHandlerBase<SaveTimeEntryUsecase, string>(actor)
{
    protected override async Task<string> ExecuteAsync(SaveTimeEntryUsecase request, CancellationToken cancellationToken)
    {
        var createId = request.Input.CreateRequestId == Guid.Empty ? Guid.NewGuid().ToString("N") : Owner + ":entry:" + request.Input.CreateRequestId.ToString("N");
        if (request.Id is null && await entries.GetBySpecAsync(new OwnedByIdSpecification<TimeEntry>(Owner, createId), cancellationToken) is { } created) return created.Id;
        var profile = await profiles.RequireAsync(Owner, Owner, cancellationToken);
        var entry = request.Id is null ? null : await entries.RequireAsync(Owner, request.Id, cancellationToken);
        if (entry is not null) OwnedDocuments.CheckVersion(entry, request.Input.Version);
        if (request.Input.End is null && (entry is null || profile.RunningEntryId != entry.Id)) throw new AppException("InvalidInterval");
        await EntryValidation.ValidateAsync(request.Input, Owner, entry?.ProjectId, projects, clock.GetUtcNow(), cancellationToken);
        var resolved = await tags.ResolveAsync(profile, request.Input.Tags, cancellationToken);
        var isNew = entry is null;
        entry ??= new TimeEntry { Id = createId, OwnerId = Owner, CompatibilityId = profile.AllocateId() };
        entry.Change(request.Input.Description, request.Input.ProjectId, resolved, request.Input.Start, request.Input.End);
        if (entry.End.HasValue && profile.RunningEntryId == entry.Id) profile.RunningEntryId = null;
        if (isNew) await entries.AddAsync(entry, cancellationToken); else await entries.UpdateAsync(entry, cancellationToken);
        await profiles.UpdateAsync(profile, cancellationToken);
        return entry.Id;
    }
}
