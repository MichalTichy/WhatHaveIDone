using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

internal sealed class RetainedImportsSpecification : IListSpecification<ImportRun>
{
    public async Task<IReadOnlyList<ImportRun>> ApplyAsync(IQueryable<ImportRun> queryable, CancellationToken cancellationToken = default)
        => await queryable.OrderByDescending(run => run.CreatedAt).ToListAsync(cancellationToken);
}
