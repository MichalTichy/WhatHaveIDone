using FastEndpoints;
using FastEndpoints.Mcp;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Tags;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class ListTagsEndpoint(IReadOnlyRepository<Tag> tags) : Endpoint<TagSearch, IReadOnlyList<string>>
{
    public override void Configure()
    {
        Get("/api/v1/tags");
        this.McpTool("list_tags", "List tags used on entries, optionally filtering by a case-insensitive search.", tool =>
        {
            tool.Hints.ReadOnly = true;
            tool.Hints.OpenWorld = false;
        });
    }

    public override async Task HandleAsync(TagSearch request, CancellationToken ct)
    {
        await Send.OkAsync(await tags.ListAsync(new TagsSpecification(request.Search), ct), ct);
    }
}
