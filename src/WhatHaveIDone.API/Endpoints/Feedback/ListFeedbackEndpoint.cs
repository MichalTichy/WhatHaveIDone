using FastEndpoints;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Feedback;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class ListFeedbackEndpoint(IReadOnlyRepository<FeedbackPost> posts) : EndpointWithoutRequest<IReadOnlyList<FeedbackModel>>
{
    public override void Configure() => Get("/api/v1/feedback");

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(await posts.ListAsync(new FeedbackSpecification(Query<int>("skip", isRequired: false)), ct), ct);
    }
}
