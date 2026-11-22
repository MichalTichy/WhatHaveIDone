using FastEndpoints;
using FastEndpoints.Mcp;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Projects;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class ListProjectsEndpoint(IReadOnlyRepository<Project> projects) : EndpointWithoutRequest<IReadOnlyList<ProjectModel>>
{
    public override void Configure()
    {
        Get("/api/v1/projects");
        this.McpTool("list_projects", "List projects and their IDs. Archived projects are unavailable for new time entries.", tool =>
        {
            tool.Hints.ReadOnly = true;
            tool.Hints.OpenWorld = false;
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(await projects.ListAsync(new ProjectsSpecification(), ct), ct);
    }
}
