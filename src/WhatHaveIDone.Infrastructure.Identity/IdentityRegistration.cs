using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.AppUser.Identity;
using Shared.Infrastructure.Authentication.Entra;
using WhatHaveIDone.Logic.Authentication;

namespace WhatHaveIDone.Infrastructure.Identity;

public static class IdentityRegistration
{
    public static IServiceCollection AddTrackingIdentity(this IServiceCollection services, IConfiguration configuration, string connectionString)
    {
        services.AddDbContext<IdentityDbContext>(o => o.UseNpgsql(connectionString, pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));
        services.AddIdentity<AppUser, UserRole>(options =>
        {
            options.SignIn.RequireConfirmedEmail = false;
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 10;
            options.Lockout.MaxFailedAccessAttempts = 5;
        }).AddEntityFrameworkStores<IdentityDbContext>().AddSignInManager();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "whid.session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
            options.Events.OnValidatePrincipal = async context =>
            {
                await SecurityStampValidator.ValidatePrincipalAsync(context);
                if (context.Principal is null) return;
                var manager = context.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
                var user = await manager.GetUserAsync(context.Principal);
                if (user is null || await manager.IsLockedOutAsync(user))
                { context.RejectPrincipal(); await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme); }
            };
        });
        services.AddScoped<IAccountAccess, AccountAccess>();
        if (!configuration.GetValue<bool>("Sso:Enabled")) return services;
        if (!Guid.TryParse(configuration["AzureAd:TenantId"], out _)) throw new InvalidOperationException("Configure a single AzureAd:TenantId for SSO.");
        services.AddExternalEntraAuthentication(configuration.GetSection("AzureAd"), "Entra", IdentityConstants.ApplicationScheme, options =>
        {
            options.CallbackPath = "/signin-oidc";
            options.ResponseType = "code"; options.UsePkce = true;
            options.Scope.Add("email");
            options.Events.OnRedirectToIdentityProvider = context => { context.ProtocolMessage.Prompt = "select_account"; return Task.CompletedTask; };
            options.Events.OnTicketReceived = async context =>
            {
                context.HandleResponse();
                try
                {
                    var principal = context.Principal!;
                    var tenant = principal.FindFirst("tid")?.Value ?? "";
                    var objectId = principal.FindFirst("oid")?.Value ?? "";
                    var email = principal.FindFirst("email")?.Value ?? principal.FindFirst("preferred_username")?.Value ?? "";
                    var name = principal.FindFirst("name")?.Value ?? email;
                    var id=await context.HttpContext.RequestServices.GetRequiredService<ISender>().Send(new CompleteSsoUsecase(tenant, objectId, email, name), context.HttpContext.RequestAborted);
                    await context.HttpContext.RequestServices.GetRequiredService<IAccountAccess>().IssueSessionAsync(id,context.HttpContext.RequestAborted);
                    context.Response.Redirect(context.Request.PathBase + "/time");
                }
                catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested) { context.HttpContext.Abort(); }
                catch (Exception)
                {
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                    context.Response.Redirect(context.Request.PathBase + "/login?error=SsoDenied");
                }
            };
            options.Events.OnRemoteFailure = context => { context.HandleResponse(); context.Response.Redirect(context.Request.PathBase + "/login?error=SsoDenied"); return Task.CompletedTask; };
            options.Events.OnAccessDenied = context => { context.HandleResponse(); context.Response.Redirect(context.Request.PathBase + "/login?error=SsoDenied"); return Task.CompletedTask; };
        });
        return services;
    }
}
