using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Infrastructure.Identity;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Authentication;

public sealed class ApiTokenHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IReadOnlyRepository<ApiToken> tokens, UserManager<AppUser> users) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiToken";
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        string secret;
        try
        {
            if (header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                var pair = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..])).Split(':', 2);
                if (pair.Length != 2 || pair[1] != "api_token") return AuthenticateResult.Fail("Invalid token.");
                secret = pair[0];
            }
            else if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) secret = header[7..];
            else return AuthenticateResult.NoResult();
        }
        catch (FormatException) { return AuthenticateResult.Fail("Invalid token."); }
        if (secret.Length > 256) return AuthenticateResult.Fail("Invalid token.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
        var token = await tokens.GetBySpecAsync(new ApiTokenSpecification(hash), Context.RequestAborted);
        if (token is null) return AuthenticateResult.Fail("Invalid token.");
        var user = await users.FindByIdAsync(token.OwnerId);
        if (user is null || await users.IsLockedOutAsync(user)) return AuthenticateResult.Fail("Invalid token.");
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Email, user.Email!)], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    { Response.StatusCode = 401; Response.Headers.WWWAuthenticate = "Basic realm=\"WhatHaveIDone\""; return Task.CompletedTask; }
}
