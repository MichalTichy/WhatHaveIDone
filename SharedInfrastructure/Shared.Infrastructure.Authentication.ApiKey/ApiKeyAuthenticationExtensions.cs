using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Infrastructure.CurrentUserProvider;
using Shared.Infrastructure.CurrentUserProvider.HttpContext;

namespace Shared.Infrastructure.Authentication.ApiKey;

public static class ApiKeyAuthenticationExtensions
{
    public static AuthenticationBuilder AddApiKeys<TUser>(this AuthenticationBuilder builder,
        Action<ApiKeyAuthenticationOptions>? configure = null) where TUser : class
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.TryAddTransient<ICurrentUserIdProvider, HttpContextCurrentUserIdProvider>();
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddScoped<ApiKeyManager>();
        builder.Services.TryAddScoped<IApiKeyValidator, IdentityApiKeyValidator<TUser>>();
        builder.Services.AddOptions<ApiKeyAuthenticationOptions>(ApiKeyAuthenticationDefaults.AuthenticationScheme)
            .ValidateDataAnnotations();
        return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationDefaults.AuthenticationScheme, configure ?? (_ => { }));
    }
}
