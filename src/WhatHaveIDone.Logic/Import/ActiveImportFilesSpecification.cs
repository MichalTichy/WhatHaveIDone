using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed class ActiveImportFilesSpecification : IListSpecification<UserProfile, string>
{
    public async Task<IReadOnlyList<string>> ApplyAsync(IQueryable<UserProfile> queryable, CancellationToken cancellationToken = default)
        => await queryable.Where(p => p.AnyTenant()).Where(p => p.Import != null && p.Import.FilePath != null &&
            p.Import.Status != "Completed" && p.Import.Status != "CompletedWithWarnings" && p.Import.Status != "Cancelled")
            .Select(p => p.Import!.FilePath!).ToListAsync(cancellationToken);
}
