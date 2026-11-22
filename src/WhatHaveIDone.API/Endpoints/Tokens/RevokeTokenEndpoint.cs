using FastEndpoints;
using MediatR;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Settings;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class RevokeTokenEndpoint(ISender sender) : Endpoint<ResourceId, bool>
{
    public override void Configure() => Delete("/api/v1/tokens/{id}");

    public override async Task HandleAsync(ResourceId request, CancellationToken ct)
    {
        await Send.OkAsync(await sender.Send(new RevokeApiTokenUsecase(request.Id), ct), ct);
    }
}
