using FastEndpoints;
using MediatR;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Settings;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class SaveSettingsEndpoint(ISender sender) : Endpoint<SettingsModel, bool>
{
    public override void Configure() => Put("/api/v1/settings");

    public override async Task HandleAsync(SettingsModel request, CancellationToken ct)
    {
        await Send.OkAsync(await sender.Send(new SaveSettingsUsecase(request), ct), ct);
    }
}
