using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;

namespace WhatHaveIDone.Infrastructure.Identity;

internal sealed class ChangePasswordEndpoint(UserManager<AppUser> users, SignInManager<AppUser> signIn) : Endpoint<ChangePasswordInput, bool>
{
    public override void Configure() => Post("/api/v1/account/password");

    public override async Task HandleAsync(ChangePasswordInput request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.GetUserAsync(User) ?? throw new AppException("Unauthenticated", 401);
        if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length > 128
            || !(await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword)).Succeeded)
            throw new AppException("InvalidPassword");
        await signIn.RefreshSignInAsync(user);
        await Send.OkAsync(true, ct);
    }
}
