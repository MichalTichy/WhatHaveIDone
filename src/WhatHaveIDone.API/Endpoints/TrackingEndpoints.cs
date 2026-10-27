using MediatR;
using Shared.Infrastructure.CurrentUserProvider;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Contracts;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Projects;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Logic.Tags;
using WhatHaveIDone.Logic.Clients;
using WhatHaveIDone.Logic.TimeEntries;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Endpoints;

public static class TrackingEndpoints
{
    public static void MapTrackingEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization();
        api.MapGet("/clients", async (ICurrentUserIdProvider actor, IReadOnlyRepository<Client> clients, CancellationToken ct) => await clients.ListAsync(new ClientsSpecification(await actor.GetCurrentUserIdRequiredAsync()), ct));
        api.MapPost("/clients", async (ClientModel input, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new SaveClientUsecase(null, input), ct)));
        api.MapPut("/clients/{id}", async (string id, ClientModel input, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new SaveClientUsecase(id, input), ct)));
        api.MapGet("/settings", async (ICurrentUserIdProvider actor, IReadOnlyRepository<UserProfile> profiles, CancellationToken ct) =>
        {
            var owner = await actor.GetCurrentUserIdRequiredAsync(); var profile = await profiles.RequireAsync(owner, owner, ct);
            return new SettingsModel { Version = profile.Version, TimeZone = profile.TimeZone, Language = profile.Language, FirstDayOfWeek = profile.FirstDayOfWeek, OnboardingCompleted = profile.OnboardingCompletedAt.HasValue };
        });
        api.MapPut("/settings", async (SettingsModel input, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new SaveSettingsUsecase(input), ct)));
        api.MapGet("/projects", async (ICurrentUserIdProvider actor, IReadOnlyRepository<Project> projects, CancellationToken ct) => await projects.ListAsync(new ProjectsSpecification(await actor.GetCurrentUserIdRequiredAsync()), ct));
        api.MapPost("/projects", async (ProjectModel input, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new SaveProjectUsecase(null, input), ct)));
        api.MapPut("/projects/{id}", async (string id, ProjectModel input, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new SaveProjectUsecase(id, input), ct)));
        api.MapGet("/projects/{id}/recent-tags", async (string id, ICurrentUserIdProvider actor, IReadOnlyRepository<Project> projects, IReadOnlyRepository<TimeEntry> entries, CancellationToken ct) =>
        {
            var owner = await actor.GetCurrentUserIdRequiredAsync();
            await projects.RequireAsync(owner, id, ct);
            return await entries.ListAsync(new RecentProjectTagsSpecification(owner, id), ct);
        });
        api.MapGet("/tags", async (string? search, ICurrentUserIdProvider actor, IReadOnlyRepository<Tag> tags, CancellationToken ct) => await tags.ListAsync(new TagsSpecification(await actor.GetCurrentUserIdRequiredAsync(), search), ct));
        api.MapGet("/time-entries", async (DateTimeOffset from, DateTimeOffset to, string? project, string? tag, string? text, ICurrentUserIdProvider actor, IReadOnlyRepository<TimeEntry> entries, CancellationToken ct) =>
        {
            if (to <= from) throw new AppException("InvalidInterval");
            return await entries.ListAsync(new TimeEntriesSpecification(await actor.GetCurrentUserIdRequiredAsync(), from, to, project, tag, text), ct);
        });
        api.MapGet("/time-entries/{id}", async (string id, ICurrentUserIdProvider actor, IReadOnlyRepository<TimeEntry> entries, CancellationToken ct) => MapEntry(await entries.RequireAsync(await actor.GetCurrentUserIdRequiredAsync(), id, ct)));
        api.MapGet("/timer", async (ICurrentUserIdProvider actor, IReadOnlyRepository<UserProfile> profiles, IReadOnlyRepository<TimeEntry> entries, CancellationToken ct) =>
        {
            var owner = await actor.GetCurrentUserIdRequiredAsync(); var profile = await profiles.RequireAsync(owner, owner, ct);
            return profile.RunningEntryId is null ? null : MapEntry(await entries.RequireAsync(owner, profile.RunningEntryId, ct));
        });
        api.MapPost("/time-entries", async (EditTimeEntry input, ISender sender, ICurrentUserIdProvider actor, IReadOnlyRepository<TimeEntry> entries, CancellationToken ct) =>
        { var id = await sender.Send(new SaveTimeEntryUsecase(null, input), ct); return MapEntry(await entries.RequireAsync(await actor.GetCurrentUserIdRequiredAsync(), id, ct)); });
        api.MapPut("/time-entries/{id}", async (string id, EditTimeEntry input, ISender sender, ICurrentUserIdProvider actor, IReadOnlyRepository<TimeEntry> entries, CancellationToken ct) =>
        { await sender.Send(new SaveTimeEntryUsecase(id, input), ct); return MapEntry(await entries.RequireAsync(await actor.GetCurrentUserIdRequiredAsync(), id, ct)); });
        api.MapDelete("/time-entries/{id}", async (string id, Guid version, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new DeleteTimeEntryUsecase(id, version), ct)));
        api.MapPost("/timer/start", async (EditTimeEntry input, ISender sender, ICurrentUserIdProvider actor, IReadOnlyRepository<TimeEntry> entries, CancellationToken ct) =>
        { var id = await sender.Send(new StartTimerUsecase(input), ct); return MapEntry(await entries.RequireAsync(await actor.GetCurrentUserIdRequiredAsync(), id, ct)); });
        api.MapPost("/timer/{id}/stop", async (string id, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new StopTimerUsecase(id), ct)));
        api.MapGet("/tokens", async (ICurrentUserIdProvider actor, IReadOnlyRepository<ApiToken> tokens, CancellationToken ct) => await tokens.ListAsync(new ApiTokensSpecification(await actor.GetCurrentUserIdRequiredAsync()), ct));
        api.MapPost("/tokens", async (TokenName input, ISender sender, CancellationToken ct) => await sender.Send(new CreateApiTokenUsecase(input.Name), ct));
        api.MapDelete("/tokens/{id}", async (string id, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new RevokeApiTokenUsecase(id), ct)));
    }

    public static TimeEntryModel MapEntry(TimeEntry entry) => new() { Id = entry.Id, CompatibilityId = entry.CompatibilityId, Version = entry.Version, Description = entry.Description, ProjectId = entry.ProjectId, Tags = entry.Tags, Start = entry.Start, End = entry.End };
}
