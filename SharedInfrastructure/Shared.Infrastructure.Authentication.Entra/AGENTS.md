# Shared.Infrastructure.Authentication.Entra

Microsoft Entra ID (OpenID Connect, Microsoft.Identity.Web) sign-in for ASP.NET Core web apps. All endpoints require an authenticated user by default. Includes a development bypass that signs in a fake user without Entra.

## Setup

```csharp
builder.Services.AddConfiguredAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddControllers(); // AuthenticationController provides GET /signout

app.UseAuthentication();
app.UseAuthorization();
```

- `AddConfiguredAuthentication` also calls `AddAuthenticatedFallbackAuthorization`: every endpoint requires authentication unless marked `[AllowAnonymous]`.
- Login failures redirect to `/login-failed` (parameter `loginFailedPath`); correlation failures (expired login) redirect to `/` (parameter `correlationFailureRedirectPath`). The app must provide an anonymous page at the failure path.
- The user prompt is always `select_account`.

## Configuration

## External sign-in alongside an application's cookie

Use `AddExternalEntraAuthentication(configuration.GetSection("AzureAd"), "Entra", existingCookieScheme, configure)` to register a named provider without changing authentication defaults, adding a cookie, installing fallback authorization or enabling the development bypass. `configure` receives `MicrosoftIdentityOptions`; use `Events.OnTicketReceived` to evaluate the validated identity before completing application sign-in. Preserve Microsoft.Identity.Web's validation events. This variant uses raw claim names and does not save provider tokens. The application owns failure handling, authorization, redirects and session creation. Never allow the external principal to become an application session without the application's access checks.


```json
"AzureAd": {
  "Instance": "https://login.microsoftonline.com/",
  "TenantId": "...",
  "ClientId": "...",
  "ClientSecret": "...",
  "CallbackPath": "/signin-oidc"
},
"Authentication": {
  "UseDevelopmentBypass": true,
  "DevelopmentBypass": {
    "UserId": "dev-user",
    "EntraId": "00000000-0000-0000-0000-000000000000",
    "Name": "Developer",
    "Email": "dev@example.com",
    "Roles": [ "Admin" ]
  }
}
```

The bypass is used only when the environment is Development **and** `Authentication:UseDevelopmentBypass` is true.

## Rules

- Never enable the development bypass outside Development; it is intentionally ignored there, don't work around that.
- The bypass must issue the same claims that `EntraUserInfo.LoadFromClaims` (project `AppUser.Entra`) reads. Keep them in sync.
- Paths passed to redirects must be path-base aware; use `OpenIdConnectFailureRedirectHelper`.

## When changing this project

Tests: `Authentication.Entra.Tests`. Changing default paths or the fallback policy affects every app using it.
