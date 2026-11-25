using FastEndpoints;
using MediatR;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Predictions;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class SaveWorkdayEndpoint(ISender sender) : Endpoint<SaveWorkdayRequest, bool>
{
    public override void Configure() => Put("/api/v1/prediction/calendar/day");

    public override async Task HandleAsync(SaveWorkdayRequest request, CancellationToken ct)
    {
        await Send.OkAsync(await sender.Send(new SaveWorkdayOverrideUsecase(request), ct), ct);
    }
}
