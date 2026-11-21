using Shared.Infrastructure.Persistence.Marten.Repository.Document;
using Shared.Infrastructure.Persistence.Marten.UnitOfWorks;

namespace Shared.Infrastructure.Authentication.ApiKey.Marten;

internal sealed class MartenApiKeyStorageService<TRecord>(INoTenancyReadOnlyRepository<TRecord> reader,
    INoTenancyRepository<TRecord> writer) : IApiKeyStorageService where TRecord : ApiKeyRecord, new()
{
    // Authentication discovers the owner before user/company tenancy is available; keys are global.
    public async Task<ApiKeyRecord?> FindActiveByHashAsync(string hash, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await reader.GetBySpecAsync(new ActiveApiKeySpecification<TRecord>(hash), cancellationToken);
    }

    public Task<IReadOnlyList<ApiKeyInfo>> ListAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return reader.ListAsync(new ApiKeyListSpecification<TRecord>(userId), cancellationToken);
    }

    public async Task AddAsync(ApiKeyRecord key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await writer.AddAsync(new TRecord { Id = key.Id, UserId = key.UserId, Name = key.Name, Hash = key.Hash,
            Suffix = key.Suffix, CreatedAt = key.CreatedAt, RevokedAt = key.RevokedAt, Version = key.Version }, cancellationToken);
    }

    public async Task<bool> RevokeAsync(string id, string userId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var unit = new UnitOfWorkProvider();
        var key = await reader.GetByIdAsync(id, cancellationToken);
        var owned = key is not null && key.UserId == userId;
        if (owned && key!.RevokedAt is null)
        {
            key.RevokedAt = revokedAt;
            await writer.UpdateAsync(key, cancellationToken);
        }
        await unit.CommitAsync(cancellationToken);
        return owned;
    }
}
