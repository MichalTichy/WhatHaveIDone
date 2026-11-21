using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace Shared.Infrastructure.Authentication.ApiKey;

public sealed class IdentityApiKeyValidator<TUser>(IApiKeyStorageService storage, UserManager<TUser> users,
    IUserClaimsPrincipalFactory<TUser> claimsFactory) : IApiKeyValidator where TUser : class
{
    public async Task<IReadOnlyCollection<Claim>?> ValidateAsync(string key, CancellationToken cancellationToken)
    {
        var record = await storage.FindActiveByHashAsync(ApiKeyHash.Compute(key), cancellationToken);
        if (record is null || record.RevokedAt is not null) return null;
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(record.UserId);
        if (user is null || (users.SupportsUserLockout && await users.IsLockedOutAsync(user))) return null;
        cancellationToken.ThrowIfCancellationRequested();
        var principal = await claimsFactory.CreateAsync(user);
        cancellationToken.ThrowIfCancellationRequested();
        return principal.Claims.ToArray();
    }
}
