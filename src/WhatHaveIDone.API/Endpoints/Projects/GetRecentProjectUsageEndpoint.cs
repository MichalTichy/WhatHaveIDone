using FastEndpoints;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Projects;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class GetRecentProjectUsageEndpoint(IReadOnlyRepository<TimeEntry> entries, TimeProvider clock)
    : EndpointWithoutRequest<Dictionary<string, double>>
{
    public override void Configure() => Get("/api/v1/projects/recent-usage");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var from = now.AddDays(-30);
        var intervals = await entries.ListAsync(new RecentProjectUsageSpecification(from, now), ct);
        Dictionary<string, double> usage = [];
        foreach (var interval in intervals)
        {
            ct.ThrowIfCancellationRequested();
            var start = interval.Start > from ? interval.Start : from;
            var end = (interval.End ?? now) < now ? interval.End ?? now : now;
            usage[interval.ProjectId] = usage.GetValueOrDefault(interval.ProjectId) + Math.Max(0, (end - start).TotalSeconds);
        }
        await Send.OkAsync(usage, ct);
    }
}
