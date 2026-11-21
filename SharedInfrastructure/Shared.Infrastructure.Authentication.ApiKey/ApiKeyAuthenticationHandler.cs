using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Shared.Infrastructure.Authentication.ApiKey;

public sealed class ApiKeyAuthenticationHandler(IOptionsMonitor<ApiKeyAuthenticationOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, IApiKeyValidator validator) : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headers = Request.Headers[Options.HeaderName];
        if (headers.Count == 0) return AuthenticateResult.NoResult();
        if (headers.Count != 1 || string.IsNullOrWhiteSpace(headers[0]) || headers[0]!.Length > 2048)
            return AuthenticateResult.Fail("Invalid API key.");

        string? key;
        if (Options.HeaderName.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
        {
            if (!AuthenticationHeaderValue.TryParse(headers[0], out var authorization))
                return AuthenticateResult.Fail("Invalid API key.");
            key = authorization.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
                ? authorization.Parameter
                : Options.ReadAdditionalKey?.Invoke(Request);
        }
        else key = headers[0];

        if (string.IsNullOrWhiteSpace(key) || key.Length > 256 || key.Any(char.IsWhiteSpace) ||
            !key.StartsWith(Options.KeyPrefix, StringComparison.Ordinal))
            return AuthenticateResult.Fail("Invalid API key.");

        var claims = await validator.ValidateAsync(key, Context.RequestAborted);
        if (claims is null) return AuthenticateResult.Fail("Invalid API key.");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers.WWWAuthenticate = Options.AdditionalChallenge?.Invoke(Request) ?? "Bearer";
        return Task.CompletedTask;
    }
}
