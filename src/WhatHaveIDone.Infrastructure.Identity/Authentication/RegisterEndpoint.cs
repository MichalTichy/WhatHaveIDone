using DataAnnotations = System.ComponentModel.DataAnnotations;
using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using Microsoft.AspNetCore.Builder;

namespace WhatHaveIDone.Infrastructure.Identity;

internal sealed class RegisterEndpoint(UserManager<AppUser> users) : Endpoint<AccountInput, string>
{
    public override void Configure()
    {
        Post("/api/v1/account/register");
        AllowAnonymous();
        Options(route => route.RequireRateLimiting("accounts"));
    }

    public override async Task HandleAsync(AccountInput request, CancellationToken ct)
    {
        if (!DataAnnotations.Validator.TryValidateObject(request, new DataAnnotations.ValidationContext(request), [], true))
            throw new AppException("InvalidAccount");
        var user = new AppUser { Email = request.Email.Trim(), UserName = request.Email.Trim(), Name = request.Email.Trim(), EmailConfirmed = false };
        if (!(await users.CreateAsync(user, request.Password)).Succeeded) throw new AppException("InvalidAccount");
        await AccountEndpoints.CompleteSignInAsync(HttpContext, user, ct);
        await Send.OkAsync(user.Id, ct);
    }
}
