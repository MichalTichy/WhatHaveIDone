using FastEndpoints;
using FastEndpoints.Mcp;
using MediatR;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.TimeEntries;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class DeleteTimeEntryEndpoint(ISender sender) : Endpoint<DeleteTimeEntryRequest, string>
{
    public override void Configure()
    {
        Delete("/api/v1/time-entries/{id}");
        this.McpTool("delete_time_entry", "Delete an entry after user confirmation. Read the latest version first.", tool =>
        {
            tool.Hints.Destructive = true;
            tool.Hints.OpenWorld = false;
        });
    }

    public override async Task HandleAsync(DeleteTimeEntryRequest request, CancellationToken ct)
    {
        await Send.OkAsync(await sender.Send(new DeleteTimeEntryUsecase(request.Id, request.Version), ct), ct);
    }
}
