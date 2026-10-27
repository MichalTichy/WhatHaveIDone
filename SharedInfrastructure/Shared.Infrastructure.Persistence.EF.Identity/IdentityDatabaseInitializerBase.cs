using Shared.Infrastructure.Initializer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.AppUser;
using Shared.Infrastructure.AppUser.Identity;

namespace Shared.Infrastructure.Persistence.EF.Identity;

public abstract class IdentityDatabaseInitializerBase<T>(AspNetIdentityDbContextBase<T> dbContext, IRoleStore<UserRole> roleStore, IUserRoleManager<UserRole> userRoleManager)
    : InitializerBase where T : UserInfoBase
{
    public override int Priority => int.MaxValue;
    public override InitializerTrigger Trigger => InitializerTrigger.OnStartup;
    public override bool RunOnlyInLeaderInstance => true;

    protected override async Task RunInitializationLogicAsync()
    {
        await EnsureDatabaseAsync();

        foreach (var userRole in userRoleManager.AllRoles)
        {
            var existingRole = await roleStore.FindByNameAsync(userRole.NormalizedName!, CancellationToken.None);
            if (existingRole != null)
            {
                continue;
            }

            await roleStore.CreateAsync(userRole, CancellationToken.None);
        }
    }

    /// <summary>
    /// Creates or upgrades the identity database. When the DbContext assembly contains EF Core migrations,
    /// they are applied (<c>MigrateAsync</c>); otherwise the schema is created with <c>EnsureCreatedAsync</c>.
    /// </summary>
    /// <remarks>
    /// A database created by <c>EnsureCreatedAsync</c> has no migration history. When adding migrations to such an app,
    /// the initial migration must be applied as a baseline (e.g. insert it into <c>__EFMigrationsHistory</c> manually),
    /// otherwise <c>MigrateAsync</c> tries to create existing tables.
    /// </remarks>
    protected virtual async Task EnsureDatabaseAsync()
    {
        if (dbContext.Database.GetMigrations().Any())
        {
            await dbContext.Database.MigrateAsync();
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync();
        }
    }
}
