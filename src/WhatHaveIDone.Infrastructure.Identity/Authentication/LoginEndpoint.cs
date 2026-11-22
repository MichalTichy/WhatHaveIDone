using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using Microsoft.AspNetCore.Builder;

namespace WhatHaveIDone.Infrastructure.Identity;

internal sealed class LoginEndpoint(UserManager<AppUser> users, SignInManager<AppUser> signIn) : Endpoint<AccountInput, string>
{
    public override void Configure()
    {
        Post("/api/v1/account/login");
        AllowAnonymous();
        Options(route => route.RequireRateLimiting("accounts"));
    }

    public override async Task HandleAsync(AccountInput request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password) || request.Email.Length > 254 || request.Password.Length > 128)
            throw new AppException("InvalidLogin", 401);
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !(await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)).Succeeded)
            throw new AppException("InvalidLogin", 401);
        await AccountEndpoints.CompleteSignInAsync(HttpContext, user, ct);
        await Send.OkAsync(user.Id, ct);
    }
}
