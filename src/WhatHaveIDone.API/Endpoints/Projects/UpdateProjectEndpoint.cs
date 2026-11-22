using FastEndpoints;
using MediatR;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Projects;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class UpdateProjectEndpoint(ISender sender) : Endpoint<ProjectModel, string>
{
    public override void Configure() => Put("/api/v1/projects/{id}");

    public override async Task HandleAsync(ProjectModel request, CancellationToken ct)
    {
        await Send.OkAsync(await sender.Send(new SaveProjectUsecase(Route<string>("id")!, request), ct), ct);
    }
}
