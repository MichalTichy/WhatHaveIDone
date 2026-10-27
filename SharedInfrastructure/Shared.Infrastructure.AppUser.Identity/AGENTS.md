# Shared.Infrastructure.AppUser.Identity

Base types for application users stored with ASP.NET Core Identity:

- `UserInfoBase`: `IdentityUser` + `IAppUser<UserRole>` with `Name` and `Roles`. Converts to and from claims (`GetClaimsIdentity`, `GetUserInfoClaims`, `LoadFromClaims`).
- `UserRole`: `IdentityRole` implementing `IUserRole`. Create instances with `UserRole._Create(name, id)` (fixed IDs, so roles can be seeded deterministically).

## Usage

```csharp
public class UserInfo : UserInfoBase
{
    public string? PhotoId { get; set; }

    public override ICollection<Claim> GetUserInfoClaims()
    {
        var claims = base.GetUserInfoClaims();
        // add claims for custom properties
        return claims;
    }

    public override void LoadFromClaims(IUserRoleManager<UserRole> userRoleManager, ICollection<Claim> claims)
    {
        base.LoadFromClaims(userRoleManager, claims);
        // read custom claims (SetClaim / GetClaimName helpers)
    }
}
```

Storage is provided by `Persistence.EF.Identity`, user management by `UserManagement`. `AppUser.Example` contains a complete sample.

## Rules

- Every custom property that the UI needs from the current user must be written in `GetUserInfoClaims` and read in `LoadFromClaims`, otherwise it is lost between requests.

## When changing this project

Used by `UserManagement`, `Persistence.EF.Identity` and apps' user classes. Changing claim names breaks existing auth cookies/tokens (users get logged out or lose data from claims).
