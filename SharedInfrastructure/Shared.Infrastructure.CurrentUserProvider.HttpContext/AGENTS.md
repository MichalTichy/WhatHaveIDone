# Shared.Infrastructure.CurrentUserProvider.HttpContext

`ICurrentUserProvider<TUser>` / `ICurrentUserIdProvider` implementations that read the user from `HttpContext.User` claims. `TUser` is loaded via `IAppUser<TRole>.LoadFromClaims`. For MVC, Web API and DotVVM apps.

## Setup

```csharp
services.AddHttpContextAccessor();
services.AddTransient<ICurrentUserProvider<UserInfo>, HttpContextCurrentUserProvider<UserInfo, UserRole>>();
services.AddTransient<ICurrentUserIdProvider, HttpContextCurrentUserIdProvider>();
```

## Rules

- Don't use it in Blazor Server components/circuits or in background jobs, where there is no `HttpContext`. Use `CurrentUserProvider.Blazor` in Blazor. Background jobs work without a current user and pass tenant/user IDs explicitly.
