using Marten;
using Shared.Infrastructure.Persistence.Specifications;

namespace Shared.Infrastructure.Authentication.ApiKey.Marten;

internal sealed class ApiKeyListSpecification<TRecord>(string userId) : IListSpecification<TRecord, ApiKeyInfo> where TRecord : ApiKeyRecord
{
    public Task<IReadOnlyList<ApiKeyInfo>> ApplyAsync(IQueryable<TRecord> queryable, CancellationToken cancellationToken = default)
        => queryable.Where(key => key.UserId == userId).OrderByDescending(key => key.CreatedAt)
            .Select(key => new ApiKeyInfo(key.Id, key.Name, key.Suffix, key.CreatedAt, key.RevokedAt)).ToListAsync(cancellationToken);
}
