using WhatHaveIDone.Logic.Execution;
using FastEndpoints;
using FastEndpoints.Mcp;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class GetTimeEntryEndpoint(IReadOnlyRepository<TimeEntry> entries) : Endpoint<ResourceId, TimeEntryModel>
{
    public override void Configure()
    {
        Get("/api/v1/time-entries/{id}");
        this.McpTool("get_time_entry", "Read the entry and its current version before updating or deleting it.", tool =>
        {
            tool.Hints.ReadOnly = true;
            tool.Hints.OpenWorld = false;
        });
    }

    public override async Task HandleAsync(ResourceId request, CancellationToken ct)
    {
        await Send.OkAsync(EntryResponse.Map(await entries.RequireAsync(request.Id, ct)), ct);
    }
}
