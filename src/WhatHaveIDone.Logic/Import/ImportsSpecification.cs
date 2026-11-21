using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed class ImportsSpecification : IListSpecification<ImportRun, ImportModel>
{
    public async Task<IReadOnlyList<ImportModel>> ApplyAsync(IQueryable<ImportRun> queryable, CancellationToken cancellationToken = default)
    {
        var runs = await queryable.OrderByDescending(run => run.CreatedAt).Take(20).ToListAsync(cancellationToken);
        return runs.Select(r => new ImportModel { Id = r.Id, Status = r.Status, Confirmed = r.Confirmed, SourceEmail = r.SourceEmail, SourceTimeZone = r.SourceTimeZone,
            Workspaces = r.Workspaces.Select(w => new ImportWorkspaceModel(w.Id,w.Name)).ToList(),
            Imported = r.Imported, Skipped = r.Skipped, Errors = r.Errors, NextAttemptAt = r.NextAttemptAt, RangeStart = r.RangeStart, RangeEnd = r.RangeEnd, RequestedFrom = r.RequestedFrom, RequestedThrough = r.RequestedThrough, MessageCode = r.MessageCode, UpdatedAt = r.UpdatedAt }).ToList();
    }
}
