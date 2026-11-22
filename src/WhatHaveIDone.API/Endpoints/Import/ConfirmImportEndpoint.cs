using FastEndpoints;
using MediatR;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Import;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class ConfirmImportEndpoint(ISender sender) : Endpoint<ConfirmImportInput, bool>
{
    public override void Configure() => Post("/api/v1/imports/{id}/confirm");

    public override async Task HandleAsync(ConfirmImportInput request, CancellationToken ct)
    {
        await Send.OkAsync(await sender.Send(new ConfirmImportUsecase(Route<string>("id")!, request), ct), ct);
    }
}
