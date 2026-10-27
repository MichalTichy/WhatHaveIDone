using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Shared.Infrastructure.Authentication.Entra;

public sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<DevelopmentAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<DevelopmentAuthenticationOptions>(options, logger, encoder)
{
    private const string ObjectIdentifierClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Options.UserId),
            new(ObjectIdentifierClaimType, Options.EntraId),
            new("name", Options.Name),
            new("preferred_username", Options.Email)
        };

        claims.AddRange(Options.Roles
            .Distinct(StringComparer.Ordinal)
            .Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, DevelopmentAuthenticationDefaults.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, DevelopmentAuthenticationDefaults.SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
