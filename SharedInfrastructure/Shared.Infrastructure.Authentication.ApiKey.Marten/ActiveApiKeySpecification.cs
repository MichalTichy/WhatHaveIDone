using Marten;
using Shared.Infrastructure.Persistence.Specifications;

namespace Shared.Infrastructure.Authentication.ApiKey.Marten;

internal sealed class ActiveApiKeySpecification<TRecord>(string hash) : ISpecification<TRecord> where TRecord : ApiKeyRecord
{
    public Task<TRecord?> ApplyAsync(IQueryable<TRecord> queryable, CancellationToken cancellationToken = default)
        => queryable.Where(key => key.Hash == hash && key.RevokedAt == null).FirstOrDefaultAsync(cancellationToken);
}
