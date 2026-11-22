using FastEndpoints;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Models;
using System.Globalization;
using System.Text.RegularExpressions;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Projects;
using WhatHaveIDone.Logic.TimeEntries;

namespace WhatHaveIDone.API.Toggl;

internal sealed class ListTimeEntriesEndpoint(IReadOnlyRepository<TimeEntry> entries, IReadOnlyRepository<Project> projects) : EndpointWithoutRequest<object>
{
    public override void Configure() => Get(TogglEndpoints.ApiPath + "/me/time_entries");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var from = ParseInstant(Query<string>("start_date")!);
        var to = ParseInstant(Query<string>("end_date")!);
        if (to <= from) throw new AppException("InvalidInterval");
        var projectIds = (await projects.ListAsync(new ProjectsSpecification(), ct)).ToDictionary(p => p.Id, p => p.CompatibilityId);
        var selected = await entries.ListAsync(new TimeEntriesSpecification(from, to, startsWithin: true), ct);
        await Send.OkAsync(selected.Select(e => new
        {
            id = e.CompatibilityId, uid = 1, user_id = 1, wid = 1, workspace_id = 1,
            pid = e.ProjectId is not null && projectIds.TryGetValue(e.ProjectId, out var pid) ? (long?)pid : null,
            project_id = e.ProjectId is not null && projectIds.TryGetValue(e.ProjectId, out var projectId) ? (long?)projectId : null,
            description = e.Description, start = e.Start, stop = e.End,
            duration = e.End.HasValue ? (long)(e.End.Value - e.Start).TotalSeconds : -e.Start.ToUnixTimeSeconds(), tags = e.Tags, billable = false
        }), ct);
    }

    private static DateTimeOffset ParseInstant(string value)
    {
        if (!Regex.IsMatch(value, "(?:[zZ]|[+-][0-9]{2}:[0-9]{2})$") || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)) throw new AppException("InvalidInterval");
        return result.ToUniversalTime();
    }
}
