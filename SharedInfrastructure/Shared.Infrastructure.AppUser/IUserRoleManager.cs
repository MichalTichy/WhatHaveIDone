namespace Shared.Infrastructure.AppUser;

public interface IUserRoleManager<TRole> where TRole : IUserRole
{
    TRole GetExistingRole(string name);
    ICollection<TRole> GetExistingRoles(ICollection<string> roles, bool throwOnUnknownRoles);
    IReadOnlyCollection<TRole> AllRoles { get; }
}
