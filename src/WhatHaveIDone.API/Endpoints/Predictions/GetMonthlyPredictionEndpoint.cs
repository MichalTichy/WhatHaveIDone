using FastEndpoints;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Predictions;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class GetMonthlyPredictionEndpoint(PredictionService predictions) : EndpointWithoutRequest<MonthlyPredictionModel>
{
    public override void Configure() => Get("/api/v1/prediction");

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(await predictions.GetMonthlyAsync(ct), ct);
    }
}
