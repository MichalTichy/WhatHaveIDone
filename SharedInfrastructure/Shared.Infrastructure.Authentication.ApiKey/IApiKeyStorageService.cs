namespace Shared.Infrastructure.Authentication.ApiKey;

public interface IApiKeyStorageService
{
    Task<ApiKeyRecord?> FindActiveByHashAsync(string hash, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApiKeyInfo>> ListAsync(string userId, CancellationToken cancellationToken);
    Task AddAsync(ApiKeyRecord key, CancellationToken cancellationToken);
    // Return false for a missing key or a different owner; repeated revocation succeeds without changing its timestamp.
    Task<bool> RevokeAsync(string id, string userId, DateTimeOffset revokedAt, CancellationToken cancellationToken);
}
