using FastEndpoints;
using MediatR;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Settings;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class CompleteOnboardingEndpoint(ISender sender) : EndpointWithoutRequest<bool>
{
    public override void Configure() => Post("/api/v1/onboarding/complete");

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(await sender.Send(new CompleteOnboardingUsecase(), ct), ct);
    }
}
