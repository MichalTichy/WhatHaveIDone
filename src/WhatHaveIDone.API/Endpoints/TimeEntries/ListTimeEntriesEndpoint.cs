using FastEndpoints;
using FastEndpoints.Mcp;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.TimeEntries;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class ListTimeEntriesEndpoint(IReadOnlyRepository<TimeEntry> entries) : Endpoint<TimeRange, IReadOnlyList<TimeEntryModel>>
{
    public override void Configure()
    {
        Get("/api/v1/time-entries");
        this.McpTool("list_time_entries", "List entries overlapping [from, to), newest first. Use ISO 8601 instants with a UTC offset. Project can be an ID or 'none'.", tool =>
        {
            tool.Hints.ReadOnly = true;
            tool.Hints.OpenWorld = false;
        });
    }

    public override async Task HandleAsync(TimeRange request, CancellationToken ct)
    {
        if (request.To <= request.From) throw new AppException("InvalidInterval");
        var specification = new TimeEntriesSpecification(request.From, request.To, request.Project, request.Tag, request.Text);
        await Send.OkAsync(await entries.ListAsync(specification, ct), ct);
    }
}
