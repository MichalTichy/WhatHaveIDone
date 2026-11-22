using FastEndpoints;
using Shared.Infrastructure.Authentication.ApiKey;
using WhatHaveIDone.API.Models;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class ListTokensEndpoint(ApiKeyManager keys) : EndpointWithoutRequest<IEnumerable<ApiTokenModel>>
{
    public override void Configure() => Get("/api/v1/tokens");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tokens = (await keys.ListAsync(ct))
            .Select(key => new ApiTokenModel(key.Id, key.Name, key.Suffix, key.CreatedAt, key.RevokedAt));
        await Send.OkAsync(tokens, ct);
    }
}
