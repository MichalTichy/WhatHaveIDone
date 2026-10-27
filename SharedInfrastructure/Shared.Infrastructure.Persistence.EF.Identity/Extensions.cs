using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.AppUser.Identity;

namespace Shared.Infrastructure.Persistence.EF.Identity;

public static class Extensions
{
    public static IdentityBuilder AddEFStores<TDbContext, TUser>(this IdentityBuilder builder) where TDbContext : AspNetIdentityDbContextBase<TUser> where TUser : UserInfoBase
    {
        return builder.AddEntityFrameworkStores<TDbContext>();
    }

    public static IServiceCollection AddEFIdentity<TDbContext, TUser>(this IServiceCollection collection,
        string connectionString, Action<DbContextOptionsBuilder>? optionsAction = null)
        where TDbContext : AspNetIdentityDbContextBase<TUser> where TUser : UserInfoBase
    {
        return collection.AddDbContext<TDbContext>(builder =>
        {
            builder.UseNpgsql(connectionString);
            optionsAction?.Invoke(builder);
        });
    }
}
