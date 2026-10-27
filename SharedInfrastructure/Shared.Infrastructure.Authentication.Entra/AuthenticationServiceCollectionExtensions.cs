using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;

namespace Shared.Infrastructure.Authentication.Entra;

public static class AuthenticationServiceCollectionExtensions
{
    private const string UseDevelopmentBypassKey = "Authentication:UseDevelopmentBypass";

    public static IServiceCollection AddExternalEntraAuthentication(
        this IServiceCollection services,
        IConfigurationSection configuration,
        string scheme,
        string signInScheme,
        Action<MicrosoftIdentityOptions>? configure = null)
    {
        services.AddAuthentication().AddMicrosoftIdentityWebApp(options =>
        {
            configuration.Bind(options);
            options.SignInScheme = signInScheme;
            options.MapInboundClaims = false;
            options.SaveTokens = false;
            configure?.Invoke(options);
        }, openIdConnectScheme: scheme, cookieScheme: null);
        return services;
    }

    public static AuthorizationBuilder AddAuthenticatedFallbackAuthorization(this IServiceCollection services)
    {
        return services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());
    }

    public static IServiceCollection AddConfiguredAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string loginFailedPath = OpenIdConnectFailureRedirectHelper.LoginFailedPath,
        string correlationFailureRedirectPath = "/")
    {
        services.AddAuthenticatedFallbackAuthorization();

        if (environment.IsDevelopment() && configuration.GetValue<bool>(UseDevelopmentBypassKey))
        {
            services
                .AddAuthentication(DevelopmentAuthenticationDefaults.SchemeName)
                .AddScheme<DevelopmentAuthenticationOptions, DevelopmentAuthenticationHandler>(
                    DevelopmentAuthenticationDefaults.SchemeName,
                    options => configuration.GetSection(DevelopmentAuthenticationOptions.SectionName).Bind(options));

            return services;
        }

        services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(options =>
            {
                configuration.Bind("AzureAd", options);
                options.Events = new OpenIdConnectEvents
                {
                    OnRedirectToIdentityProvider = context =>
                    {
                        context.ProtocolMessage.Prompt = "select_account";
                        return Task.CompletedTask;
                    },
                    OnRemoteFailure = context => OpenIdConnectFailureRedirectHelper.HandleRemoteFailureAsync(
                        context,
                        loginFailedPath,
                        correlationFailureRedirectPath),
                    OnAuthenticationFailed = context =>
                    {
                        context.HandleResponse();
                        context.Response.Redirect(loginFailedPath);
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }
}
