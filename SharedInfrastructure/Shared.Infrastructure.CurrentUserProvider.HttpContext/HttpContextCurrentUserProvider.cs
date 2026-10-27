using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Shared.Infrastructure.AppUser;

namespace Shared.Infrastructure.CurrentUserProvider.HttpContext;

public class HttpContextCurrentUserProvider<TUser, TRole>(IHttpContextAccessor httpContextAccessor, IUserRoleManager<TRole> userRoleManager) : ICurrentUserProvider<TUser>
    where TUser : class, IAppUser<TRole>, new() where TRole : IUserRole
{
    public Task<TUser?> GetCurrentUserAsync()
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext?.User?.Claims == null) return Task.FromResult<TUser?>(null);

        var claims = httpContext.User.Claims.ToArray();
        var user = httpContext.User.Identity!.IsAuthenticated ? GetFromClaims(claims):null;

        return Task.FromResult(user);
    }

    private TUser? GetFromClaims(Claim[] claims)
    {
        var user = new TUser();
        user.LoadFromClaims(userRoleManager,claims);
        return user;
    }

    public async Task<TUser> GetCurrentUserRequiredAsync()
    {
        return await GetCurrentUserAsync() ?? throw new NoLoggedUserFoundException();
    }
}
