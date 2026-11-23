using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Projects;

internal sealed class ProjectUsageSpecification(string id) : ISpecification<TimeEntry, bool>
{
    public Task<bool> ApplyAsync(IQueryable<TimeEntry> query, CancellationToken cancellationToken = default)
        => query.AnyAsync(item => item.ProjectId == id, cancellationToken);
}
