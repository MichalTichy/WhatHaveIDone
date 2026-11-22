using WhatHaveIDone.Logic.Execution;
using FastEndpoints;
using FastEndpoints.Mcp;
using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.API.Mcp;
using WhatHaveIDone.Logic.TimeEntries;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class StopTimerEndpoint(ISender sender, IReadOnlyRepository<TimeEntry> entries) : Endpoint<ResourceId, TimeEntryModel>
{
    public override void Configure()
    {
        Post("/api/v1/timer/{id}/stop");
        this.McpTool("stop_timer", "Stop the specified timer after user review. Repeating a stop never stops another timer.", tool =>
        {
            tool.Hints.Destructive = true;
            tool.Hints.Idempotent = true;
            tool.Hints.OpenWorld = false;
        });
    }

    public override async Task HandleAsync(ResourceId request, CancellationToken ct)
    {
        var stopped = await sender.Send(new StopTimerUsecase(request.Id), ct);
        if (!McpInvocation.IsTool(HttpContext))
        {
            await Send.ResultAsync(Results.Ok(stopped));
            return;
        }

        await Send.OkAsync(EntryResponse.Map(await entries.RequireAsync(request.Id, ct)), ct);
    }
}
