using FastEndpoints;
using MediatR;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Clients;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class UpdateClientEndpoint(ISender sender) : Endpoint<ClientModel, string>
{
    public override void Configure() => Put("/api/v1/clients/{id}");

    public override async Task HandleAsync(ClientModel request, CancellationToken ct)
    {
        await Send.OkAsync(await sender.Send(new SaveClientUsecase(Route<string>("id")!, request), ct), ct);
    }
}
