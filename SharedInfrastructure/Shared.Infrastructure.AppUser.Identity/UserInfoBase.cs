using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace Shared.Infrastructure.AppUser.Identity;

public abstract class UserInfoBase : IdentityUser, IAppUser<UserRole>
{
    protected UserInfoBase()
    {
    }

    protected UserInfoBase(string id, string email, string name, ICollection<UserRole> roles)
    {
        Id = id;
        Email = email;
        Name = name;
        Roles = roles;
    }

    public ICollection<UserRole> Roles { get; set; } = [];
    public virtual void LoadFromClaims(IUserRoleManager<UserRole> userRoleManager, ICollection<Claim> claims)
    {
        Id = claims.Single(t => t.Type == ClaimTypes.NameIdentifier).Value;
        Name = claims.Single(t => t.Type == ClaimTypes.GivenName).Value;
        Email = claims.Single(t => t.Type == ClaimTypes.Email).Value;
        Roles = claims.Where(t => t.Type == ClaimTypes.Role).Select(claim => userRoleManager.GetExistingRole(claim.Value)).ToList();
    }
    public string Name { get; set; } = null!;

    protected virtual string GetClaimName(string propertyName)
    {
        return $"custom-claim-{propertyName.ToLower()}";
    }

    protected string? SetClaim(ICollection<Claim> claims, string targetName)
    {
        var value = claims.FirstOrDefault(t => t.Type == targetName)?.Value;
        return !string.IsNullOrWhiteSpace(value) ? value : null;
    }

    public virtual ClaimsPrincipal GetClaimsIdentity()
    {
        var claims = GetUserInfoClaims();

        claims.Add(new Claim(ClaimTypes.NameIdentifier, Id));
        claims.Add(new Claim(ClaimTypes.Email, Email!));
        foreach (var userRole in Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, userRole.Name!));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "ClaimTransfer"));
    }

    public virtual ICollection<Claim> GetUserInfoClaims()
    {
        List<Claim> claims =
        [
            new(ClaimTypes.GivenName, Name)
        ];
        return claims;
    }
}
