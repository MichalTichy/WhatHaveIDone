using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

// Privileged discovery only. Claiming and writing happen inside the document's tenant.
public sealed class RunnableImportsSpecification(DateTimeOffset now) : IListSpecification<ImportRun>
{
    public async Task<IReadOnlyList<ImportRun>> ApplyAsync(IQueryable<ImportRun> queryable, CancellationToken cancellationToken = default)
        => await queryable.Where(run => run.AnyTenant())
            .Where(run => (run.Status == ImportStatus.Queued || run.Status == ImportStatus.Running || run.Status == ImportStatus.Waiting)
                && (run.NextAttemptAt == null || run.NextAttemptAt <= now)
                && (run.LeaseUntil == null || run.LeaseUntil <= now))
            .OrderBy(run => run.UpdatedAt).Take(8).ToListAsync(cancellationToken);
}
