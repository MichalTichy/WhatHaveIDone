using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.TimeEntries;

public sealed record StopTimerUsecase(string Id) : IRequest<bool>;

public sealed class StopTimerUsecaseHandler(IRepository<UserProfile> profiles, IRepository<TimeEntry> entries, TimeProvider clock)
    : UsecaseHandlerBase<StopTimerUsecase, bool>
{
    protected override async Task<bool> ExecuteAsync(StopTimerUsecase request, CancellationToken cancellationToken)
    {
        var entry = await entries.RequireAsync(request.Id, cancellationToken);
        if (entry.End.HasValue) return false;
        var profile = await profiles.RequireAsync(new UserProfileSpecification(), cancellationToken);
        if (profile.RunningEntryId != entry.Id) throw new AppException("Conflict", 409);
        var now = clock.GetUtcNow();
        entry.End = now >= entry.Start ? now : entry.Start;
        profile.RunningEntryId = null;
        await entries.UpdateAsync(entry, cancellationToken); await profiles.UpdateAsync(profile, cancellationToken);
        return true;
    }
}
