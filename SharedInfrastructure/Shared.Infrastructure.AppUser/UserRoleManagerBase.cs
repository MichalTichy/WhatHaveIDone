using System.Xml.Linq;

namespace Shared.Infrastructure.AppUser;

public abstract class UserRoleManagerBase<TRole> : IUserRoleManager<TRole> where TRole : IUserRole
{
    protected UserRoleManagerBase(ICollection<TRole> supportedRoles)
    {
        foreach (var supportedRole in supportedRoles)
        {
            AddRole(supportedRole);
        }
    }

    private List<TRole> supportedRoles { get; } = [];
    public ICollection<TRole> GetExistingRoles(ICollection<string> roles, bool throwOnUnknownRoles)
    {
        var foundRoles = new List<TRole>();
        foreach (var roleName in roles)
        {
            var role = supportedRoles.FirstOrDefault(t => t.Name == roleName);

            if (role != null)
            {
                foundRoles.Add(role);
            }

            if (throwOnUnknownRoles)
            {
                throw new ArgumentException($"Unknown role: {roleName}");
            }
        }

        return foundRoles;
    }

    public IReadOnlyCollection<TRole> AllRoles => supportedRoles.AsReadOnly();

    private void AddRole(TRole role)
    {
        supportedRoles.Add(role);
    }
    public TRole GetExistingRole(string name)
    {
        var role = supportedRoles.FirstOrDefault(t => t.Name == name);

        if (role!=null)
        {
            return role;
        }

        throw new ArgumentException($"Unknown role: {name}");
    }
}
