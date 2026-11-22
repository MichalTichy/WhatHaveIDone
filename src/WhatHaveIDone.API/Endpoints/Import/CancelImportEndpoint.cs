using FastEndpoints;
using MediatR;
using WhatHaveIDone.Logic.Import;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class CancelImportEndpoint(ISender sender) : EndpointWithoutRequest<bool>
{
    public override void Configure() => Post("/api/v1/imports/{id}/cancel");

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(await sender.Send(new CancelImportUsecase(Route<string>("id")!), ct), ct);
    }
}
