using WhatHaveIDone.Logic.Execution;
using FastEndpoints;
using FastEndpoints.Mcp;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class GetSettingsEndpoint(IReadOnlyRepository<UserProfile> profiles) : EndpointWithoutRequest<SettingsModel>
{
    public override void Configure()
    {
        Get("/api/v1/settings");
        this.McpTool("get_settings", "Read the userÃ¢â‚¬â„¢s time zone, language and first day of the week.", tool =>
        {
            tool.Hints.ReadOnly = true;
            tool.Hints.OpenWorld = false;
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var profile = await profiles.RequireAsync(new UserProfileSpecification(), ct);
        await Send.OkAsync(new SettingsModel
        {
            Version = profile.SettingsVersion,
            TimeZone = profile.TimeZone,
            Language = profile.Language,
            FirstDayOfWeek = profile.FirstDayOfWeek,
            OnboardingCompleted = profile.OnboardingCompletedAt.HasValue
        }, ct);
    }
}
