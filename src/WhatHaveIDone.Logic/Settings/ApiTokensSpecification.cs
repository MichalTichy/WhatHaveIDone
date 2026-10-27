using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.Contracts;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Settings;

public sealed class ApiTokensSpecification(string owner) : IListSpecification<ApiToken, ApiTokenModel>
{
    public async Task<IReadOnlyList<ApiTokenModel>> ApplyAsync(IQueryable<ApiToken> queryable, CancellationToken cancellationToken = default)
        => await queryable.Where(t => t.OwnerId == owner).OrderByDescending(t => t.CreatedAt)
            .Select(t => new ApiTokenModel(t.Id, t.Name, t.Suffix, t.CreatedAt, t.RevokedAt)).ToListAsync(cancellationToken);
}
