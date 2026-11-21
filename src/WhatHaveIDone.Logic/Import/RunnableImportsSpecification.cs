using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

// Privileged worker discovery only; all subsequent processing uses the profile's tenant.
public sealed class RunnableImportsSpecification(DateTimeOffset now) : IListSpecification<UserProfile>
{
    public async Task<IReadOnlyList<UserProfile>> ApplyAsync(IQueryable<UserProfile> queryable, CancellationToken cancellationToken = default)
        => await queryable.Where(p => p.AnyTenant()).Where(p => p.Import != null &&
            (p.Import.Status == "Queued" || p.Import.Status == "Running" || p.Import.Status == "Waiting") &&
            (p.Import.NextAttemptAt == null || p.Import.NextAttemptAt <= now))
            .OrderBy(p => p.Import!.UpdatedAt).Take(1).ToListAsync(cancellationToken);
}
