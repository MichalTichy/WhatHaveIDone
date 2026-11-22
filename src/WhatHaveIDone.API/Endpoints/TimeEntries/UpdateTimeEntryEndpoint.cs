using WhatHaveIDone.Logic.Execution;
using FastEndpoints;
using FastEndpoints.Mcp;
using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.TimeEntries;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class UpdateTimeEntryEndpoint(ISender sender, IReadOnlyRepository<TimeEntry> entries) : Endpoint<UpdateTimeEntryRequest, TimeEntryModel>
{
    public override void Configure()
    {
        Put("/api/v1/time-entries/{id}");
        this.McpTool("update_time_entry", "Replace the entryÃ¢â‚¬â„¢s editable fields after user review. Supply its latest version. Preserve existing values where appropriate.", tool =>
        {
            tool.Hints.Destructive = true;
            tool.Hints.OpenWorld = false;
        });
    }

    public override async Task HandleAsync(UpdateTimeEntryRequest request, CancellationToken ct)
    {
        await sender.Send(new SaveTimeEntryUsecase(request.Id, request), ct);
        await Send.OkAsync(EntryResponse.Map(await entries.RequireAsync(request.Id, ct)), ct);
    }
}
