using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

using Shared.Infrastructure.AppUser.Identity;

namespace Shared.Infrastructure.Persistence.EF.Identity;

public abstract class AspNetIdentityDbContextBase<TUser> : IdentityDbContext<TUser, UserRole, string> where TUser : UserInfoBase
{
    protected AspNetIdentityDbContextBase(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<TUser>(typeBuilder => typeBuilder.Ignore(user => user.Roles)); // roles are filled in later by UserManager
    }
}
