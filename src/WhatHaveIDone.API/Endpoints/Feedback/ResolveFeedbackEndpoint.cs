using FastEndpoints;
using MediatR;
using WhatHaveIDone.Logic.Feedback;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class ResolveFeedbackEndpoint(ISender sender) : EndpointWithoutRequest<bool>
{
    public override void Configure() => Post("/api/v1/feedback/{id}/resolve");

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(await sender.Send(new ResolveFeedbackUsecase(Route<string>("id")!), ct), ct);
    }
}
