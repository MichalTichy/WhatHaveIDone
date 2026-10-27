# Shared.Infrastructure.Persistence.EF.Identity

Stores ASP.NET Core Identity users (`UserInfoBase` subclasses from `AppUser.Identity`) in PostgreSQL with EF Core:

- `AspNetIdentityDbContextBase<TUser>`: Identity DbContext with `UserRole` roles.
- `AddEFIdentity<TDbContext, TUser>(connectionString)`: registers the DbContext (Npgsql).
- `identityBuilder.AddEFStores<TDbContext, TUser>()`: registers EF stores for Identity.
- `IdentityDatabaseInitializerBase<TUser>`: leader-only initializer that creates/upgrades the database and seeds all roles from `IUserRoleManager`.

`AppUser.Example` contains a complete wiring.

## Usage

```csharp
public class AuthDbContext(DbContextOptions<AuthDbContext> options) : AspNetIdentityDbContextBase<UserInfo>(options);
public class AuthDbInitializer(AuthDbContext db, IRoleStore<UserRole> roles, IUserRoleManager<UserRole> roleManager)
    : IdentityDatabaseInitializerBase<UserInfo>(db, roles, roleManager);

services.AddEFIdentity<AuthDbContext, UserInfo>(configuration.GetConnectionString("Auth")!);
services.AddIdentityCore<UserInfo>().AddRoles<UserRole>().AddEFStores<AuthDbContext, UserInfo>();
```

## Rules

- The initializer applies EF Core migrations (`MigrateAsync`) when the DbContext assembly has any, otherwise it calls `EnsureCreatedAsync()`. When adding migrations to an app whose database was created by `EnsureCreated`, apply the initial migration as a baseline first (insert it into `__EFMigrationsHistory`). Override `EnsureDatabaseAsync()` for custom behavior.
- `UserInfoBase.Roles` is ignored by EF; roles are stored in the Identity role tables and filled by `UserManager`.
