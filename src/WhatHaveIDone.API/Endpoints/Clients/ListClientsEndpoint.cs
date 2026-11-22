using FastEndpoints;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Clients;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class ListClientsEndpoint(IReadOnlyRepository<Client> clients) : EndpointWithoutRequest<IReadOnlyList<ClientModel>>
{
    public override void Configure() => Get("/api/v1/clients");

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(await clients.ListAsync(new ClientsSpecification(), ct), ct);
    }
}
