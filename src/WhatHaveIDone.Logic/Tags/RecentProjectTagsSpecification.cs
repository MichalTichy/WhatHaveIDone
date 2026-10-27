using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Tags;

public sealed class RecentProjectTagsSpecification(string owner, string projectId) : IListSpecification<TimeEntry, string>
{
    public async Task<IReadOnlyList<string>> ApplyAsync(IQueryable<TimeEntry> queryable, CancellationToken cancellationToken = default)
    {
        var recent = await queryable.Where(e => e.OwnerId == owner && e.ProjectId == projectId && e.Tags.Any())
            .OrderByDescending(e => e.Start).ThenByDescending(e => e.CompatibilityId).Take(20)
            .Select(e => e.Tags).ToListAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return recent.SelectMany(tags => tags).Select(t => t.Trim()).Where(t => t.Length > 0)
            .DistinctBy(Tag.Normalize).Take(8).ToArray();
    }
}
