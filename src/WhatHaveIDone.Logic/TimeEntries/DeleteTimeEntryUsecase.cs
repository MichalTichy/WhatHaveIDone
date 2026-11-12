using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.TimeEntries;

public sealed record DeleteTimeEntryUsecase(string Id, Guid Version) : IRequest<string>;

public sealed class DeleteTimeEntryUsecaseHandler(IRepository<UserProfile> profiles, IRepository<TimeEntry> entries)
    : UsecaseHandlerBase<DeleteTimeEntryUsecase, string>
{
    protected override async Task<string> ExecuteAsync(DeleteTimeEntryUsecase request, CancellationToken cancellationToken)
    {
        var entry = await entries.RequireAsync(request.Id, cancellationToken);
        Documents.CheckVersion(entry, request.Version);
        var profile = await profiles.RequireAsync(new UserProfileSpecification(), cancellationToken);
        if (profile.RunningEntryId == entry.Id)
        {
            profile.RunningEntryId = null;
            await profiles.UpdateAsync(profile, cancellationToken);
        }
        await entries.DeleteAsync(entry, cancellationToken);
        return entry.Id;
    }
}
