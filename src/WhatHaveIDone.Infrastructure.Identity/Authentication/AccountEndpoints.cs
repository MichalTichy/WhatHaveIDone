using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.Persistence.Marten.UnitOfWorks;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Infrastructure.Identity;

public static class AccountEndpoints
{
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

    internal static async Task CompleteSignInAsync(HttpContext http, AppUser user, CancellationToken ct)
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
