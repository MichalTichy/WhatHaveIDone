using FastEndpoints;
using MediatR;
using WhatHaveIDone.Logic.Projects;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class DeleteProjectEndpoint(ISender sender) : Endpoint<DeleteVersionedDocumentRequest, bool>
{
    public override void Configure() => Delete("/api/v1/projects/{id}");

    public override async Task HandleAsync(DeleteVersionedDocumentRequest request, CancellationToken ct)
    {
        await Send.OkAsync(await sender.Send(new DeleteProjectUsecase(request.Id, request.Version), ct), ct);
    }
}
