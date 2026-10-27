using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;

namespace Shared.Infrastructure.AppUser.Identity;

#pragma warning disable CS8766 // Nullability of reference types in return type doesn't match implicitly implemented member (possibly because of nullability attributes).
public sealed class UserRole : IdentityRole, IUserRole
#pragma warning restore CS8766 // Nullability of reference types in return type doesn't match implicitly implemented member (possibly because of nullability attributes).
{
    [JsonConstructor]
    public UserRole()
    {
    }

    private UserRole(string name, string? id = null) : base(name)
    {
        NormalizedName = name.ToUpper();
        if (id != null)
        {
            Id = id;
        }
    }

    /// <summary>
    ///     Should be used only from role manager
    /// </summary>
    /// <returns></returns>
    public static UserRole _Create(string name, string id)
    {
        return new UserRole(name, id);
    }
}
