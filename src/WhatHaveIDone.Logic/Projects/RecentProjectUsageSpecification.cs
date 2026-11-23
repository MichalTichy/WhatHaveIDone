using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Projects;

public sealed class RecentProjectUsageSpecification(DateTimeOffset from, DateTimeOffset to)
    : IListSpecification<TimeEntry, ProjectInterval>
{
    public async Task<IReadOnlyList<ProjectInterval>> ApplyAsync(IQueryable<TimeEntry> entries, CancellationToken cancellationToken = default)
        => await entries.Where(e => e.ProjectId != null && e.Start < to && (e.End == null || e.End > from))
            .Select(e => new ProjectInterval(e.ProjectId!, e.Start, e.End))
            .ToListAsync(cancellationToken);
}
