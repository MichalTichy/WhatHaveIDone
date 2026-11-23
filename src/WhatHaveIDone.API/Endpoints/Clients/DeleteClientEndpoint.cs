using FastEndpoints;
using MediatR;
using WhatHaveIDone.Logic.Clients;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class DeleteClientEndpoint(ISender sender) : Endpoint<DeleteVersionedDocumentRequest, bool>
{
    public override void Configure() => Delete("/api/v1/clients/{id}");

    public override async Task HandleAsync(DeleteVersionedDocumentRequest request, CancellationToken ct)
    {
        await Send.OkAsync(await sender.Send(new DeleteClientUsecase(request.Id, request.Version), ct), ct);
    }
}
