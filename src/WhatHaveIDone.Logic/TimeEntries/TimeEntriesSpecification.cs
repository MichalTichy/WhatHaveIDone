using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.Contracts;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.TimeEntries;

public sealed class TimeEntriesSpecification(string owner, DateTimeOffset from, DateTimeOffset to, string? project = null, string? tag = null, string? text = null, bool startsWithin = false)
    : IListSpecification<TimeEntry, TimeEntryModel>
{
    public async Task<IReadOnlyList<TimeEntryModel>> ApplyAsync(IQueryable<TimeEntry> queryable, CancellationToken cancellationToken = default)
    {
        var query = queryable.Where(e => e.OwnerId == owner && e.Start < to);
        query = startsWithin ? query.Where(e => e.Start >= from) : query.Where(e => e.End == null || e.End > from || e.End == e.Start && e.Start >= from);
        if (project == "none") query = query.Where(e => e.ProjectId == null);
        else if (!string.IsNullOrWhiteSpace(project)) query = query.Where(e => e.ProjectId == project);
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var name = tag.Trim();
            query = query.Where(e => e.Tags.Any(value => value.Equals(name, StringComparison.OrdinalIgnoreCase)));
        }
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(e => e.Description.Contains(text, StringComparison.OrdinalIgnoreCase));
        return await query.OrderByDescending(e => e.Start).Select(e => new TimeEntryModel
        {
            Id = e.Id, CompatibilityId = e.CompatibilityId, Version = e.Version, Description = e.Description,
            ProjectId = e.ProjectId, Tags = e.Tags, Start = e.Start, End = e.End
        }).ToListAsync(cancellationToken);
    }
}
