using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Shared.Infrastructure.CurrentUserProvider.HttpContext;

public class HttpContextCurrentUserIdProvider(IHttpContextAccessor httpContextAccessor) : ICurrentUserIdProvider
{
    public Task<string?> GetCurrentUserIdAsync()
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext?.User?.Claims == null) return Task.FromResult((string?)null);

        var claims = httpContext.User.Claims.ToArray();

        var id = claims.SingleOrDefault(t => t.Type == ClaimTypes.NameIdentifier)?.Value;

        return Task.FromResult(id);
    }

    public async Task<string> GetCurrentUserIdRequiredAsync()
    {
        var idOrNull = await GetCurrentUserIdAsync();
        return idOrNull 
            ?? throw new InvalidOperationException("Cannot get current user ID.");
    }
}
