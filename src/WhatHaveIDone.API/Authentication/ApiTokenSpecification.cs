using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Authentication;

public sealed class ApiTokenSpecification(string hash) : ISpecification<ApiToken>
{
    public Task<ApiToken?> ApplyAsync(IQueryable<ApiToken> queryable, CancellationToken cancellationToken = default)
        => queryable.Where(t => t.Hash == hash && t.RevokedAt == null).FirstOrDefaultAsync(cancellationToken);
}
