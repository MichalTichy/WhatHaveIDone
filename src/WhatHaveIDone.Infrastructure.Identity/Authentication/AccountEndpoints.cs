using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Persistence.Marten.UnitOfWorks;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Infrastructure.Identity;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/session", async (HttpContext http, IAntiforgery csrf, UserManager<AppUser> users, IConfiguration config) =>
        {
            http.Response.Headers.CacheControl = "no-cache, no-store"; http.Response.Headers.Pragma = "no-cache";
            var user = http.User.Identity?.IsAuthenticated == true ? await users.GetUserAsync(http.User) : null;
            return new SessionModel(user is not null, user?.Email, user is not null && await users.HasPasswordAsync(user), config.GetValue<bool>("Sso:Enabled"), csrf.GetAndStoreTokens(http).RequestToken);
        }).AllowAnonymous();
        app.MapPost("/api/v1/account/register", RegisterAsync).AllowAnonymous().RequireRateLimiting("accounts");
        app.MapPost("/api/v1/account/login", SignInAsync).AllowAnonymous().RequireRateLimiting("accounts");
        app.MapPost("/api/v1/account/logout", async (SignInManager<AppUser> signIn, CancellationToken ct) =>
        {
            ct.ThrowIfCancellationRequested();
            await signIn.SignOutAsync();
            return Results.Ok(true);
        }).RequireAuthorization();
        app.MapPost("/api/v1/account/password", ChangePasswordAsync).RequireAuthorization();
        app.MapGet("/auth/sso", (IConfiguration config) => config.GetValue<bool>("Sso:Enabled")
            ? Results.Challenge(new AuthenticationProperties { RedirectUri = "/time" }, ["Entra"]) : Results.NotFound()).AllowAnonymous();
    }

    private static async Task<IResult> RegisterAsync(AccountInput input, HttpContext http, UserManager<AppUser> users, CancellationToken ct)
    {
        if (!Validator.TryValidateObject(input, new ValidationContext(input), [], true)) throw new AppException("InvalidAccount");
        var user = new AppUser { Email = input.Email.Trim(), UserName = input.Email.Trim(), Name = input.Email.Trim(), EmailConfirmed = false };
        if (!(await users.CreateAsync(user, input.Password)).Succeeded) throw new AppException("InvalidAccount");
        await CompleteSignInAsync(http, user, ct);
        return Results.Ok(user.Id);
    }

    private static async Task<IResult> SignInAsync(AccountInput input, HttpContext http, UserManager<AppUser> users, SignInManager<AppUser> signIn, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrEmpty(input.Password) || input.Email.Length > 254 || input.Password.Length > 128)
            throw new AppException("InvalidLogin", 401);
        var user = await users.FindByEmailAsync(input.Email.Trim());
        if (user is null || !(await signIn.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: true)).Succeeded)
            throw new AppException("InvalidLogin", 401);
        await CompleteSignInAsync(http, user, ct);
        return Results.Ok(user.Id);
    }

    private static async Task<IResult> ChangePasswordAsync(ChangePasswordInput input, HttpContext http, UserManager<AppUser> users, SignInManager<AppUser> signIn, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.GetUserAsync(http.User) ?? throw new AppException("Unauthenticated", 401);
        if (string.IsNullOrEmpty(input.CurrentPassword) || string.IsNullOrEmpty(input.NewPassword) || input.NewPassword.Length > 128
            || !(await users.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword)).Succeeded)
            throw new AppException("InvalidPassword");
        await signIn.RefreshSignInAsync(user);
        return Results.Ok(true);
    }

    public static async Task HandleSsoTicketAsync(TicketReceivedContext context)
    {
        context.HandleResponse();
        var http = context.HttpContext; var ct = http.RequestAborted;
        try
        {
            // OIDC validates the provider ticket before invoking this event.
            var principal = context.Principal!;
            var tenant = principal.FindFirst("tid")?.Value ?? "";
            var objectId = principal.FindFirst("oid")?.Value ?? "";
            var email = principal.FindFirst("email")?.Value ?? principal.FindFirst("preferred_username")?.Value ?? "";
            var name = principal.FindFirst("name")?.Value ?? email;
            var config = http.RequestServices.GetRequiredService<IConfiguration>();
            if (!Guid.TryParse(tenant, out _) || !Guid.TryParse(objectId, out _) || !config.GetValue<bool>("Sso:Enabled")
                || !string.Equals(tenant, config["AzureAd:TenantId"], StringComparison.OrdinalIgnoreCase)) throw new AppException("SsoDenied", 403);
            var users = http.RequestServices.GetRequiredService<UserManager<AppUser>>();
            var key = tenant.ToLowerInvariant() + ":" + objectId.ToLowerInvariant();
            var user = await users.FindByLoginAsync("Entra", key);
            if (user is null)
            {
                if (!new EmailAddressAttribute().IsValid(email) || await users.FindByEmailAsync(email) is not null) throw new AppException("SsoAccountConflict", 409);
                var database = http.RequestServices.GetRequiredService<IdentityDbContext>();
                await using var transaction = await database.Database.BeginTransactionAsync(ct);
                user = new AppUser { Email = email, UserName = email, Name = name, EmailConfirmed = false };
                if (!(await users.CreateAsync(user)).Succeeded || !(await users.AddLoginAsync(user, new UserLoginInfo("Entra", key, "Microsoft"))).Succeeded)
                    throw new AppException("SsoDenied", 403);
                await transaction.CommitAsync(ct);
            }
            await CompleteSignInAsync(http, user, ct);
            http.Response.Redirect(http.Request.PathBase + "/time");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { http.Abort(); }
        catch (Exception)
        {
            await http.SignOutAsync(IdentityConstants.ApplicationScheme);
            http.Response.Redirect(http.Request.PathBase + "/login?error=SsoDenied");
        }
    }

    private static async Task CompleteSignInAsync(HttpContext http, AppUser user, CancellationToken ct)
    {
        var services = http.RequestServices;
        var database = services.GetRequiredService<IdentityDbContext>();
        user = await database.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == user.Id, ct) ?? throw new AppException("InvalidLogin", 401);
        var users = services.GetRequiredService<UserManager<AppUser>>();
        var signIn = services.GetRequiredService<SignInManager<AppUser>>();
        if (!await signIn.CanSignInAsync(user) || await users.IsLockedOutAsync(user)) throw new AppException("InvalidLogin", 401);
        await using (var transaction = new UnitOfWorkProvider())
        {
            var profiles = services.GetRequiredService<IRepository<UserProfile>>();
            if (await profiles.GetByIdAsync(user.Id, ct, tenantId: user.Id) is null)
            {
                var profile = new UserProfile { Id = user.Id };
                var client = Client.CreateDefault(user.Id, profile.AllocateId());
                profile.DefaultClientId = client.Id;
                await services.GetRequiredService<IRepository<Client>>().AddAsync(client, ct, tenantId: user.Id);
                await profiles.AddAsync(profile, ct, tenantId: user.Id);
            }
            await transaction.CommitAsync(ct);
        }
        // Commit application data before issuing a usable session.
        ct.ThrowIfCancellationRequested();
        await signIn.SignInAsync(user, isPersistent: false);
        http.User = await signIn.CreateUserPrincipalAsync(user);
    }
}
