using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Shared.Infrastructure.CurrentUserProvider;

namespace Shared.Infrastructure.Authentication.ApiKey;

public sealed class ApiKeyManager(IApiKeyStorageService storage, ICurrentUserIdProvider actor,
    TimeProvider clock, IOptionsMonitor<ApiKeyAuthenticationOptions> options)
{
    public async Task<TemporaryApiKey> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100) throw new ArgumentException("Provide a name of at most 100 characters.", nameof(name));
        var userId = await actor.GetCurrentUserIdRequiredAsync();
        var key = options.Get(ApiKeyAuthenticationDefaults.AuthenticationScheme).KeyPrefix +
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var record = new ApiKeyRecord { Id = Guid.NewGuid().ToString("N"), UserId = userId, Name = name.Trim(),
            Hash = ApiKeyHash.Compute(key), Suffix = key[^6..], CreatedAt = clock.GetUtcNow() };
        await storage.AddAsync(record, cancellationToken);
        return new TemporaryApiKey(record.Id, key);
    }

    public async Task<IReadOnlyList<ApiKeyInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await storage.ListAsync(await actor.GetCurrentUserIdRequiredAsync(), cancellationToken);
    }

    public async Task<bool> RevokeAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await storage.RevokeAsync(id, await actor.GetCurrentUserIdRequiredAsync(), clock.GetUtcNow(), cancellationToken);
    }
}
