using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Execution;

public sealed class OwnedByIdSpecification<T>(string owner, string id) : ISpecification<T> where T : OwnedDocument
{
    public Task<T?> ApplyAsync(IQueryable<T> queryable, CancellationToken cancellationToken = default)
        => queryable.Where(x => x.OwnerId == owner && x.Id == id).FirstOrDefaultAsync(cancellationToken);
}
