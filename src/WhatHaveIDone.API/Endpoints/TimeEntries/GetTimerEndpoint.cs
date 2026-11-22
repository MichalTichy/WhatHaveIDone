using WhatHaveIDone.Logic.Execution;
using FastEndpoints;
using FastEndpoints.Mcp;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class GetTimerEndpoint(IReadOnlyRepository<UserProfile> profiles, IReadOnlyRepository<TimeEntry> entries) : EndpointWithoutRequest<TimeEntryModel?>
{
    public override void Configure()
    {
        Get("/api/v1/timer");
        this.McpTool("get_timer", "Read the current running timer. A null response means there is no running timer.", tool =>
        {
            tool.Hints.ReadOnly = true;
            tool.Hints.OpenWorld = false;
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var profile = await profiles.RequireAsync(new UserProfileSpecification(), ct);
        if (profile.RunningEntryId is null)
        {
            await Send.StringAsync("null", contentType: "application/json", cancellation: ct);
            return;
        }

        await Send.OkAsync(EntryResponse.Map(await entries.RequireAsync(profile.RunningEntryId, ct)), ct);
    }
}
