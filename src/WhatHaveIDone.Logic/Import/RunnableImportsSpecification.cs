using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

// Privileged discovery only. The worker reloads and writes each run inside its tenant.
public sealed class RunnableImportsSpecification(DateTimeOffset now) : IListSpecification<ImportRun>
{
    public async Task<IReadOnlyList<ImportRun>> ApplyAsync(IQueryable<ImportRun> queryable, CancellationToken cancellationToken = default)
        => await queryable.Where(run => run.AnyTenant())
            .Where(run => (run.Status == ImportStatus.Queued || run.Status == ImportStatus.Running || run.Status == ImportStatus.Waiting)
                && (run.NextAttemptAt == null || run.NextAttemptAt <= now))
            .OrderBy(run => run.UpdatedAt).Take(8).ToListAsync(cancellationToken);
}
