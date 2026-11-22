using FastEndpoints;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class HealthEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/health");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.ResultAsync(Results.Ok(new { status = "ok" }));
}
