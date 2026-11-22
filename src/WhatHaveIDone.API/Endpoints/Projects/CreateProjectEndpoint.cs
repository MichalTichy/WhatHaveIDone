using FastEndpoints;
using MediatR;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Projects;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class CreateProjectEndpoint(ISender sender) : Endpoint<ProjectModel, string>
{
    public override void Configure() => Post("/api/v1/projects");

    public override async Task HandleAsync(ProjectModel request, CancellationToken ct)
    {
        await Send.OkAsync(await sender.Send(new SaveProjectUsecase(null, request), ct), ct);
    }
}
