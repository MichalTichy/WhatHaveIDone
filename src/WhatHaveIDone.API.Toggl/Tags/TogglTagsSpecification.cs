using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Toggl.Tags;

public sealed class TogglTagsSpecification(string owner, string? search = null) : IListSpecification<Tag, TogglTag>
{
    public async Task<IReadOnlyList<TogglTag>> ApplyAsync(IQueryable<Tag> queryable, CancellationToken cancellationToken = default)
    {
        var query = queryable.Where(t => t.OwnerId == owner);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = Tag.Normalize(search);
            query = query.Where(t => t.NormalizedName.Contains(normalized));
        }
        return await query.OrderBy(t => t.NormalizedName).Select(t => new TogglTag(t.CompatibilityId, t.Name)).ToListAsync(cancellationToken);
    }
}
