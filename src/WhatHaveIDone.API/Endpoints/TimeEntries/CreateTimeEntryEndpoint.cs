using FastEndpoints;
using FastEndpoints.Mcp;
using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.API.Mcp;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.TimeEntries;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class CreateTimeEntryEndpoint(ISender sender, IReadOnlyRepository<TimeEntry> entries) : Endpoint<EditTimeEntry, TimeEntryModel>
{
    public override void Configure()
    {
        Post("/api/v1/time-entries");
        this.McpTool("create_time_entry", "Create a confirmed entry after user review. Supply a nonempty createRequestId UUID and reuse it on retry. Start and end require a UTC offset.", tool =>
        {
            tool.Hints.Idempotent = true;
            tool.Hints.Destructive = false;
            tool.Hints.OpenWorld = false;
        });
    }

    public override async Task HandleAsync(EditTimeEntry request, CancellationToken ct)
    {
        if (McpInvocation.IsTool(HttpContext))
        {
            if (request.CreateRequestId == Guid.Empty) throw new AppException("InvalidRequest");
            request.Tags ??= [];
        }

        var id = await sender.Send(new SaveTimeEntryUsecase(null, request), ct);
        await Send.OkAsync(EntryResponse.Map(await entries.RequireAsync(id, ct)), ct);
    }
}
