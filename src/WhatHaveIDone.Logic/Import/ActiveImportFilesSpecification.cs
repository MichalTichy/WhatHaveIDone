using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed class ActiveImportFilesSpecification : IListSpecification<ImportRun, string>
{
    public async Task<IReadOnlyList<string>> ApplyAsync(IQueryable<ImportRun> queryable, CancellationToken cancellationToken = default)
        => await queryable.Where(r => r.FilePath != null && r.Status != "Completed" && r.Status != "CompletedWithWarnings" && r.Status != "Cancelled")
            .Select(r => r.FilePath!).ToListAsync(cancellationToken);
}
