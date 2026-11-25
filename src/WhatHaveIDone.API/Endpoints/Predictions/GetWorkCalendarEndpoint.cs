using FastEndpoints;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Predictions;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class GetWorkCalendarEndpoint(PredictionService predictions) : EndpointWithoutRequest<WorkCalendarModel>
{
    public override void Configure() => Get("/api/v1/prediction/calendar/{year:int}/{month:int}");

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(await predictions.GetCalendarAsync(Route<int>("year"), Route<int>("month"), ct), ct);
    }
}
