using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed class ImportsSpecification : IListSpecification<UserProfile, ImportModel>
{
    public async Task<IReadOnlyList<ImportModel>> ApplyAsync(IQueryable<UserProfile> queryable, CancellationToken cancellationToken = default)
    {
        var profile = await queryable.SingleOrDefaultAsync(cancellationToken);
        if (profile is null) return [];
        var runs = (profile.Import is { } current ? new[] { current } : []).Concat(profile.ImportHistory).Take(20);
        return runs.Select(r => new ImportModel { Id = r.Id, Kind = r.Kind, Status = r.Status, Stage = r.Stage, Confirmed = r.Confirmed, SourceEmail = r.SourceEmail, SourceTimeZone = r.SourceTimeZone,
            AvailableEmails = r.AvailableEmails, Workspaces = r.Workspaces.Select(w => new ImportWorkspaceModel(w.Id,w.Name)).ToList(), TotalRows = r.TotalRows, PreviewErrors = r.PreviewErrors, HasOriginalIds = r.HasOriginalIds,
            Preview = r.Preview.Select(p=>new ImportPreviewRow(p.Email,p.Description,p.Project,p.Start,p.End)).ToList(),
            Imported = r.Imported, Skipped = r.Skipped, Errors = r.Errors, NextAttemptAt = r.NextAttemptAt, RangeStart = r.RangeStart, RangeEnd = r.RangeEnd, MessageCode = r.MessageCode, UpdatedAt = r.UpdatedAt }).ToList();
    }
}
