using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;

namespace Shared.Infrastructure.Authentication.Entra;

[Route("/signout")]
[ApiController]
public class AuthenticationController(IAuthenticationSchemeProvider authenticationSchemeProvider) : Controller
{
    [HttpGet]
    public async Task<IActionResult> SignOutAsync()
    {
        var hasDevelopmentScheme = await authenticationSchemeProvider.GetSchemeAsync(DevelopmentAuthenticationDefaults.SchemeName) is not null;
        var hasCookieScheme = await authenticationSchemeProvider.GetSchemeAsync(CookieAuthenticationDefaults.AuthenticationScheme) is not null;
        var hasOpenIdConnectScheme = await authenticationSchemeProvider.GetSchemeAsync(OpenIdConnectDefaults.AuthenticationScheme) is not null;
        var redirectUri = GetPostSignOutRedirectUri();

        if (hasDevelopmentScheme && !hasOpenIdConnectScheme)
        {
            return LocalRedirect(redirectUri);
        }

        var schemes = new List<string>();
        if (hasCookieScheme)
        {
            schemes.Add(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        if (hasOpenIdConnectScheme)
        {
            schemes.Add(OpenIdConnectDefaults.AuthenticationScheme);
        }

        return schemes.Count == 0
            ? LocalRedirect(redirectUri)
            : SignOut(
                new AuthenticationProperties
                {
                    RedirectUri = redirectUri
                },
                schemes.ToArray());
    }

    private string GetPostSignOutRedirectUri() =>
        Request.PathBase.HasValue
            ? $"{Request.PathBase}/"
            : "/";
}
