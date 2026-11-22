using FastEndpoints;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Models;
using WhatHaveIDone.Logic.Clients;

namespace WhatHaveIDone.API.Toggl;

internal sealed class ListClientsEndpoint(IReadOnlyRepository<Client> clients) : EndpointWithoutRequest<object>
{
    public override void Configure() => Get(TogglEndpoints.ApiPath + "/workspaces/{workspaceId}/clients");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var items = await clients.ListAsync(new ClientsSpecification(), ct);
        await Send.OkAsync(items.Select(c => new { id = c.CompatibilityId, name = c.Name, wid = 1, workspace_id = 1 }), ct);
    }
}
