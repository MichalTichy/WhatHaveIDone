using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Shared.Infrastructure.CurrentUserProvider;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Projects;
using WhatHaveIDone.Logic.Tags;
using WhatHaveIDone.Logic.Clients;
using WhatHaveIDone.Logic.TimeEntries;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Toggl;

public static class TogglEndpoints
{
    internal const string ApiPath = "/api/v9";

    public static void MapTogglEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup(ApiPath).RequireAuthorization();
        api.MapGet("/workspaces", () => new[] { new { id = 1, name = "Personal" } });
        api.MapGet("/workspaces/{workspaceId}/clients", async (ICurrentUserIdProvider actor, IReadOnlyRepository<Client> clients, CancellationToken ct)
            => (await clients.ListAsync(new ClientsSpecification(await actor.GetCurrentUserIdRequiredAsync()), ct)).Select(c => new { id = c.CompatibilityId, name = c.Name, wid = 1, workspace_id = 1 }));
        api.MapGet("/workspaces/{workspaceId}/projects", async (ICurrentUserIdProvider actor, IReadOnlyRepository<Project> projects, IReadOnlyRepository<Client> clients, CancellationToken ct) =>
        {
            var owner = await actor.GetCurrentUserIdRequiredAsync();
            var clientIds = (await clients.ListAsync(new ClientsSpecification(owner), ct)).ToDictionary(c => c.Id, c => c.CompatibilityId);
            return (await projects.ListAsync(new ProjectsSpecification(owner), ct)).Select(p => new
            {
                id = p.CompatibilityId, name = p.Name, color = p.Color, active = !p.Archived, wid = 1, workspace_id = 1,
                cid = p.ClientId is not null && clientIds.TryGetValue(p.ClientId, out var cid) ? (long?)cid : null,
                client_id = p.ClientId is not null && clientIds.TryGetValue(p.ClientId, out var clientId) ? (long?)clientId : null
            });
        });
        api.MapGet("/workspaces/{workspaceId}/tags", async (string? search, ICurrentUserIdProvider actor, IReadOnlyRepository<Tag> tags, CancellationToken ct)
            => (await tags.ListAsync(new TagsSpecification(await actor.GetCurrentUserIdRequiredAsync(), search), ct)).Select(t => new { id = t.Id, name = t.Name, wid = 1, workspace_id = 1 }));
        api.MapGet("/me/time_entries", async (string start_date, string end_date, ICurrentUserIdProvider actor, IReadOnlyRepository<TimeEntry> entries, IReadOnlyRepository<Project> projects, CancellationToken ct) =>
        {
            var from = ParseInstant(start_date); var to = ParseInstant(end_date);
            if (to <= from) throw new AppException("InvalidInterval");
            var owner = await actor.GetCurrentUserIdRequiredAsync();
            var projectIds = (await projects.ListAsync(new ProjectsSpecification(owner), ct)).ToDictionary(p => p.Id, p => p.CompatibilityId);
            var selected = await entries.ListAsync(new TimeEntriesSpecification(owner, from, to, startsWithin: true), ct);
            return selected.Select(e => new
            {
                id = e.CompatibilityId, uid = 1, user_id = 1, wid = 1, workspace_id = 1,
                pid = e.ProjectId is not null && projectIds.TryGetValue(e.ProjectId, out var pid) ? (long?)pid : null,
                project_id = e.ProjectId is not null && projectIds.TryGetValue(e.ProjectId, out var projectId) ? (long?)projectId : null,
                description = e.Description, start = e.Start, stop = e.End,
                duration = e.End.HasValue ? (long)(e.End.Value - e.Start).TotalSeconds : -e.Start.ToUnixTimeSeconds(), tags = e.Tags, billable = false
            });
        });
    }
    private static DateTimeOffset ParseInstant(string value)
    {
        if (!Regex.IsMatch(value, "(?:[zZ]|[+-][0-9]{2}:[0-9]{2})$") || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)) throw new AppException("InvalidInterval");
        return result.ToUniversalTime();
    }
}
