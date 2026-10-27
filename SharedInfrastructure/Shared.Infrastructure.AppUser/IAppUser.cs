using System.Security.Claims;

namespace Shared.Infrastructure.AppUser;

public interface IAppUser<TRole> where TRole : IUserRole
{
    string Id { get; set; }
    string Name { get; set; }
    string? Email { get; set; }
    ICollection<TRole> Roles { get; set; }

    public void LoadFromClaims(IUserRoleManager<TRole> userRoleManager, ICollection<Claim> claims);
}
