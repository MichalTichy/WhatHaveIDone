using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using WhatHaveIDone.API.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;

namespace WhatHaveIDone.Infrastructure.Identity;

internal sealed class SsoChallengeEndpoint(IConfiguration config) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/auth/sso");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.ResultAsync(config.GetValue<bool>("Sso:Enabled")
            ? Results.Challenge(new AuthenticationProperties { RedirectUri = "/time" }, ["Entra"])
            : Results.NotFound());
}
