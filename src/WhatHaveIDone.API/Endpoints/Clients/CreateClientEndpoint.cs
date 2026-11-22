using FastEndpoints;
using MediatR;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Clients;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class CreateClientEndpoint(ISender sender) : Endpoint<ClientModel, string>
{
    public override void Configure() => Post("/api/v1/clients");

    public override async Task HandleAsync(ClientModel request, CancellationToken ct)
    {
        await Send.OkAsync(await sender.Send(new SaveClientUsecase(null, request), ct), ct);
    }
}
