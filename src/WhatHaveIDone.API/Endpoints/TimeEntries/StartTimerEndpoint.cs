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

internal sealed class StartTimerEndpoint(ISender sender, IReadOnlyRepository<TimeEntry> entries) : Endpoint<StartTimerRequest, TimeEntryModel>
{
    public override void Configure()
    {
        Post("/api/v1/timer/start");
        this.McpTool("start_timer", "Start a timer after user review, atomically stopping any running timer. Repeating this tool starts another timer.", tool =>
        {
            tool.Hints.Destructive = true;
            tool.Hints.Idempotent = false;
            tool.Hints.OpenWorld = false;
        });
    }

    public override async Task HandleAsync(StartTimerRequest request, CancellationToken ct)
    {
        var input = new EditTimeEntry
        {
            Description = request.Description,
            ProjectId = request.ProjectId,
            Tags = McpInvocation.IsTool(HttpContext) ? request.Tags ?? [] : request.Tags
        };
        var id = await sender.Send(new StartTimerUsecase(input), ct);
        await Send.OkAsync(EntryResponse.Map(await entries.RequireAsync(id, ct)), ct);
    }
}
