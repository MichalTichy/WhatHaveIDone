using WhatHaveIDone.Logic.Execution;
using FastEndpoints;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Tags;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class ListRecentProjectTagsEndpoint(IReadOnlyRepository<Project> projects, IReadOnlyRepository<TimeEntry> entries) : Endpoint<ResourceId, IReadOnlyList<string>>
{
    public override void Configure() => Get("/api/v1/projects/{id}/recent-tags");

    public override async Task HandleAsync(ResourceId request, CancellationToken ct)
    {
        await projects.RequireAsync(request.Id, ct);
        await Send.OkAsync(await entries.ListAsync(new RecentProjectTagsSpecification(request.Id), ct), ct);
    }
}
