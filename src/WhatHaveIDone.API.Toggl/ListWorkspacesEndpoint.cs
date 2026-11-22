using FastEndpoints;

namespace WhatHaveIDone.API.Toggl;

internal sealed class ListWorkspacesEndpoint : EndpointWithoutRequest<object>
{
    public override void Configure() => Get(TogglEndpoints.ApiPath + "/workspaces");

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(new[] { new { id = 1, name = "Personal" } }, ct);
    }
}
