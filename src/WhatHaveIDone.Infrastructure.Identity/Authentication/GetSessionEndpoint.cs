using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using WhatHaveIDone.API.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Configuration;

namespace WhatHaveIDone.Infrastructure.Identity;

internal sealed class GetSessionEndpoint(IAntiforgery csrf, UserManager<AppUser> users, IConfiguration config)
    : EndpointWithoutRequest<SessionModel>
{
    public override void Configure()
    {
        Get("/api/v1/session");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-cache, no-store";
        HttpContext.Response.Headers.Pragma = "no-cache";
        var user = User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(User) : null;
        await Send.OkAsync(new SessionModel(user is not null, user?.Email,
            user is not null && await users.HasPasswordAsync(user),
            config.GetValue<bool>("Sso:Enabled"), csrf.GetAndStoreTokens(HttpContext).RequestToken), ct);
    }
}
