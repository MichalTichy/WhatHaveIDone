using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.Contracts;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Clients;

public sealed class ClientsSpecification(string owner) : IListSpecification<Client, ClientModel>
{
    public async Task<IReadOnlyList<ClientModel>> ApplyAsync(IQueryable<Client> queryable, CancellationToken cancellationToken = default)
        => await queryable.Where(c => c.OwnerId == owner).OrderByDescending(c => c.IsDefault).ThenBy(c => c.Name)
            .Select(c => new ClientModel { Id = c.Id, CompatibilityId = c.CompatibilityId, Name = c.Name, Version = c.Version, IsDefault = c.IsDefault }).ToListAsync(cancellationToken);
}
