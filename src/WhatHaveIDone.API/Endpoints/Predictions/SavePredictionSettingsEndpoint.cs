using FastEndpoints;
using MediatR;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Predictions;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class SavePredictionSettingsEndpoint(ISender sender) : Endpoint<PredictionSettingsModel, PredictionSettingsModel>
{
    public override void Configure() => Put("/api/v1/prediction/settings");

    public override async Task HandleAsync(PredictionSettingsModel request, CancellationToken ct)
    {
        await Send.OkAsync(await sender.Send(new SavePredictionSettingsUsecase(request), ct), ct);
    }
}
