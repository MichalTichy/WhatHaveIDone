using FastEndpoints;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Models;
using WhatHaveIDone.Logic.Clients;
using WhatHaveIDone.Logic.Projects;

namespace WhatHaveIDone.API.Toggl;

internal sealed class ListProjectsEndpoint(IReadOnlyRepository<Project> projects, IReadOnlyRepository<Client> clients) : EndpointWithoutRequest<object>
{
    public override void Configure() => Get(TogglEndpoints.ApiPath + "/workspaces/{workspaceId}/projects");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var clientIds = (await clients.ListAsync(new ClientsSpecification(), ct)).ToDictionary(c => c.Id, c => c.CompatibilityId);
        await Send.OkAsync((await projects.ListAsync(new ProjectsSpecification(), ct)).Select(p => new
        {
            id = p.CompatibilityId, name = p.Name, color = p.Color, active = !p.Archived, wid = 1, workspace_id = 1,
            cid = p.ClientId is not null && clientIds.TryGetValue(p.ClientId, out var cid) ? (long?)cid : null,
            client_id = p.ClientId is not null && clientIds.TryGetValue(p.ClientId, out var clientId) ? (long?)clientId : null
        }), ct);
    }
}
