using MediatR;
using Shared.Infrastructure.CurrentUserProvider;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Tags;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.TimeEntries;

public sealed record StartTimerUsecase(EditTimeEntry Input) : IRequest<string>;

public sealed class StartTimerUsecaseHandler(ICurrentUserIdProvider actor, IRepository<UserProfile> profiles, IRepository<TimeEntry> entries,
    IReadOnlyRepository<Project> projects, TagCatalog tags, TimeProvider clock) : OwnerUsecaseHandlerBase<StartTimerUsecase, string>(actor)
{
    protected override async Task<string> ExecuteAsync(StartTimerUsecase request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var input = request.Input with { Start = now, End = null };
        await EntryValidation.ValidateAsync(input, Owner, null, projects, now, cancellationToken);
        var profile = await profiles.RequireAsync(Owner, Owner, cancellationToken);
        if (profile.RunningEntryId is not null)
        {
            var previous = await entries.RequireAsync(Owner, profile.RunningEntryId, cancellationToken);
            now = now >= previous.Start ? now : previous.Start;
            previous.End = now;
            await entries.UpdateAsync(previous, cancellationToken);
        }
        var entry = new TimeEntry { Id = Guid.NewGuid().ToString("N"), OwnerId = Owner, CompatibilityId = profile.AllocateId() };
        entry.Change(input.Description, input.ProjectId, await tags.ResolveAsync(profile, input.Tags, cancellationToken), now, null);
        await entries.AddAsync(entry, cancellationToken);
        profile.RunningEntryId = entry.Id;
        await profiles.UpdateAsync(profile, cancellationToken);
        return entry.Id;
    }
}
