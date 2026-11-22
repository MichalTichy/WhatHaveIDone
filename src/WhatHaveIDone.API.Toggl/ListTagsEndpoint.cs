using FastEndpoints;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Models;
using WhatHaveIDone.API.Toggl.Tags;

namespace WhatHaveIDone.API.Toggl;

internal sealed class ListTagsEndpoint(IReadOnlyRepository<Tag> tags) : EndpointWithoutRequest<object>
{
    public override void Configure() => Get(TogglEndpoints.ApiPath + "/workspaces/{workspaceId}/tags");

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(await tags.ListAsync(new TogglTagsSpecification(Query<string>("search", isRequired: false)), ct), ct);
    }
}
