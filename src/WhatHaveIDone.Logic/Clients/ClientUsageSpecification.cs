using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Clients;

internal sealed class ClientUsageSpecification(string id) : ISpecification<Project, bool>
{
    public Task<bool> ApplyAsync(IQueryable<Project> query, CancellationToken cancellationToken = default)
        => query.AnyAsync(item => item.ClientId == id, cancellationToken);
}
