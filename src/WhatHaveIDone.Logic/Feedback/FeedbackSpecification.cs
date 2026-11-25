using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Feedback;

public sealed class FeedbackSpecification(int skip) : IListSpecification<FeedbackPost, FeedbackModel>
{
    public async Task<IReadOnlyList<FeedbackModel>> ApplyAsync(IQueryable<FeedbackPost> queryable, CancellationToken cancellationToken = default)
        => await queryable.Where(p => p.ResolvedAt == null).OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip(Math.Max(0, skip)).Take(50)
            .Select(p => new FeedbackModel(p.Id, p.Text, p.CreatedAt)).ToListAsync(cancellationToken);
}
