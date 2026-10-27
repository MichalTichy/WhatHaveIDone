using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed class RunnableImportsSpecification(DateTimeOffset now) : IListSpecification<ImportRun>
{
    public async Task<IReadOnlyList<ImportRun>> ApplyAsync(IQueryable<ImportRun> queryable, CancellationToken cancellationToken = default)
        => await queryable.Where(r => (r.Status == "Queued" || r.Status == "Running" || r.Status == "Waiting") && (r.NextAttemptAt == null || r.NextAttemptAt <= now))
            .OrderBy(r => r.UpdatedAt).Take(1).ToListAsync(cancellationToken);
}
