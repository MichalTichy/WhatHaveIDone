using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Settings;

public sealed class UserProfileSpecification : ISpecification<UserProfile>
{
    public Task<UserProfile?> ApplyAsync(IQueryable<UserProfile> queryable, CancellationToken cancellationToken = default)
        => queryable.SingleOrDefaultAsync(cancellationToken);
}
