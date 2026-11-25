using FastEndpoints;
using MediatR;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Feedback;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class SubmitFeedbackEndpoint(ISender sender) : Endpoint<SubmitFeedback, string>
{
    public override void Configure() => Post("/api/v1/feedback");

    public override async Task HandleAsync(SubmitFeedback request, CancellationToken ct)
    {
        await Send.OkAsync(await sender.Send(new SubmitFeedbackUsecase(request.Text), ct), ct);
    }
}
