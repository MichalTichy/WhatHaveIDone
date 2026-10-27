using MediatR;
using Shared.Infrastructure.CurrentUserProvider;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.TimeEntries;

public sealed record StopTimerUsecase(string Id) : IRequest<bool>;

public sealed class StopTimerUsecaseHandler(ICurrentUserIdProvider actor, IRepository<UserProfile> profiles, IRepository<TimeEntry> entries, TimeProvider clock)
    : OwnerUsecaseHandlerBase<StopTimerUsecase, bool>(actor)
{
    protected override async Task<bool> ExecuteAsync(StopTimerUsecase request, CancellationToken cancellationToken)
    {
        var entry = await entries.RequireAsync(Owner, request.Id, cancellationToken);
        if (entry.End.HasValue) return false;
        var profile = await profiles.RequireAsync(Owner, Owner, cancellationToken);
        if (profile.RunningEntryId != entry.Id) throw new AppException("Conflict", 409);
        var now = clock.GetUtcNow();
        entry.End = now >= entry.Start ? now : entry.Start;
        profile.RunningEntryId = null;
        await entries.UpdateAsync(entry, cancellationToken); await profiles.UpdateAsync(profile, cancellationToken);
        return true;
    }
}
