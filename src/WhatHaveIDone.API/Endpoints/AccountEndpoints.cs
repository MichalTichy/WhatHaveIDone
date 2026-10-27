using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Shared.Infrastructure.CurrentUserProvider;
using WhatHaveIDone.Contracts;
using WhatHaveIDone.Infrastructure.Identity;
using WhatHaveIDone.Logic.Authentication;
using WhatHaveIDone.Logic.Settings;

namespace WhatHaveIDone.API.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/session", async (HttpContext http, IAntiforgery csrf, UserManager<AppUser> users, IConfiguration config) =>
        {
            http.Response.Headers.CacheControl="no-cache, no-store";http.Response.Headers.Pragma="no-cache";
            var user = http.User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(http.User) : null;
            return new SessionModel(user is not null, user?.Email, user is not null && await users.HasPasswordAsync(user), config.GetValue<bool>("Sso:Enabled"), csrf.GetAndStoreTokens(http).RequestToken);
        }).AllowAnonymous();
        app.MapPost("/api/v1/account/register", async (AccountInput input, ISender sender, IAccountAccess accounts, CancellationToken ct) =>
        { var id=await sender.Send(new RegisterAccountUsecase(input),ct);await accounts.IssueSessionAsync(id,ct);return Results.Ok(id); }).AllowAnonymous().RequireRateLimiting("accounts");
        app.MapPost("/api/v1/account/login", async (AccountInput input, ISender sender, IAccountAccess accounts, CancellationToken ct) =>
        { var id=await sender.Send(new SignInUsecase(input),ct);await accounts.IssueSessionAsync(id,ct);return Results.Ok(id); }).AllowAnonymous().RequireRateLimiting("accounts");
        app.MapPost("/api/v1/account/logout", async (ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new SignOutUsecase(), ct))).RequireAuthorization();
        app.MapPost("/api/v1/account/password", async (ChangePasswordInput input, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new ChangePasswordUsecase(input), ct))).RequireAuthorization();
        app.MapPost("/api/v1/onboarding/complete", async (ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new CompleteOnboardingUsecase(), ct))).RequireAuthorization();
        app.MapGet("/auth/sso", (IConfiguration config) => config.GetValue<bool>("Sso:Enabled")
            ? Results.Challenge(new AuthenticationProperties { RedirectUri = "/time" }, ["Entra"]) : Results.NotFound()).AllowAnonymous();
    }
}
