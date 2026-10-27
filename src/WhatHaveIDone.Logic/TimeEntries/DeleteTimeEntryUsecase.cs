using MediatR;
using Shared.Infrastructure.CurrentUserProvider;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.TimeEntries;

public sealed record DeleteTimeEntryUsecase(string Id, Guid Version) : IRequest<string>;

public sealed class DeleteTimeEntryUsecaseHandler(ICurrentUserIdProvider actor, IRepository<UserProfile> profiles, IRepository<TimeEntry> entries, IRepository<ImportExclusion> exclusions)
    : OwnerUsecaseHandlerBase<DeleteTimeEntryUsecase, string>(actor)
{
    protected override async Task<string> ExecuteAsync(DeleteTimeEntryUsecase request, CancellationToken cancellationToken)
    {
        var entry = await entries.RequireAsync(Owner, request.Id, cancellationToken);
        OwnedDocuments.CheckVersion(entry, request.Version);
        var profile = await profiles.RequireAsync(Owner, Owner, cancellationToken);
        if (profile.RunningEntryId == entry.Id) profile.RunningEntryId = null;
        if (entry.SourceKey is not null)
            await exclusions.AddAsync(new ImportExclusion { Id = entry.Id, OwnerId = Owner, SourceKey = entry.SourceKey }, cancellationToken);
        await entries.DeleteAsync(entry, cancellationToken); await profiles.UpdateAsync(profile, cancellationToken);
        return entry.Id;
    }
}
