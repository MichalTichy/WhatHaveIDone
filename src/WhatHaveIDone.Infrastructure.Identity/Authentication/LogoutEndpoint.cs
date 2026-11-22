using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using WhatHaveIDone.API.Models;

namespace WhatHaveIDone.Infrastructure.Identity;

internal sealed class LogoutEndpoint(SignInManager<AppUser> signIn) : EndpointWithoutRequest<bool>
{
    public override void Configure() => Post("/api/v1/account/logout");

    public override async Task HandleAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await signIn.SignOutAsync();
        await Send.OkAsync(true, ct);
    }
}
