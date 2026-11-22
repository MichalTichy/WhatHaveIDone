using FastEndpoints;
using MediatR;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Settings;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class CreateTokenEndpoint(ISender sender) : Endpoint<CreateApiKeyRequest, CreatedApiToken>
{
    public override void Configure() => Post("/api/v1/tokens");

    public override async Task HandleAsync(CreateApiKeyRequest request, CancellationToken ct)
    {
        await Send.OkAsync(await sender.Send(new CreateApiTokenUsecase(request.Name), ct), ct);
    }
}
