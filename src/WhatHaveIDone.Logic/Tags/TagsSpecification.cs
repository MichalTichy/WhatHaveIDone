using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.Contracts;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Tags;

public sealed class TagsSpecification(string owner, string? search = null) : IListSpecification<Tag, TagModel>
{
    public async Task<IReadOnlyList<TagModel>> ApplyAsync(IQueryable<Tag> queryable, CancellationToken cancellationToken = default)
    {
        var query = queryable.Where(t => t.OwnerId == owner);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = Tag.Normalize(search);
            query = query.Where(t => t.NormalizedName.Contains(normalized));
        }
        return await query.OrderBy(t => t.NormalizedName).Select(t => new TagModel(t.CompatibilityId, t.Name)).ToListAsync(cancellationToken);
    }
}
