using Marten;
using Microsoft.Extensions.DependencyInjection;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Predictions;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Api.Tests;

[Collection("postgres")]
public sealed class ReportStatisticsApiTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task StatisticsRespectLocalTodayClippingFiltersAndOwnershipAsync()
    {
        using var owner = await fixture.CreateOwnerAsync();
        using var other = await fixture.CreateOwnerAsync();
        const string zoneId = "Pacific/Kiritimati";
        var settings = await owner.GetAsync<SettingsModel>("settings");
        await owner.PutAsync<bool>("settings", settings with { TimeZone = zoneId });
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(fixture.Clock.GetUtcNow(), zone).DateTime);
        var midnight = WorkCalendarRules.DayStart(today, zone);
        var store = fixture.App.Services.GetRequiredService<IDocumentStore>();
        await using (var session = store.LightweightSession(owner.Owner))
        {
            session.Insert(new TimeEntry
            {
                Id = "midnight", CompatibilityId = 100, Start = midnight.AddHours(-1), End = midnight.AddHours(1), Tags = ["selected"]
            });
            session.Insert(new TimeEntry
            {
                Id = "other-tag", CompatibilityId = 101, Start = midnight.AddHours(-1), End = midnight.AddHours(8), Tags = ["other"]
            });
            session.Insert(new TimeEntry
            {
                Id = "future", CompatibilityId = 102, Start = midnight.AddDays(1), End = midnight.AddDays(1).AddHours(8), Tags = ["selected"]
            });
            await session.SaveChangesAsync();
        }
        await using (var session = store.LightweightSession(other.Owner))
        {
            session.Insert(new TimeEntry
            {
                Id = "other-owner", CompatibilityId = 100, Start = midnight.AddHours(-1), End = midnight.AddHours(10), Tags = ["selected"]
            });
            await session.SaveChangesAsync();
        }

        var from = Uri.EscapeDataString(midnight.AddMinutes(-30).ToString("O"));
        var to = Uri.EscapeDataString(midnight.AddDays(2).ToString("O"));
        var report = await owner.GetAsync<ReportModel>($"reports?from={from}&to={to}&tag=selected");
        var statistics = report.Statistics.ToDictionary(s => s.Name, s => s.Value);

        Assert.Equal(9.5 * 3600, report.TotalSeconds);
        Assert.Equal(3, report.Days.Count);
        Assert.Equal(1800, statistics[StatisticKind.AverageWorkedDay]);
        Assert.Equal(1800, statistics[StatisticKind.TotalTrackedTime]);
        Assert.Equal(1, statistics[StatisticKind.WorkedDays]);
        Assert.Equal(1800, statistics[StatisticKind.MedianWorkedDay]);
        Assert.Equal(1, statistics[StatisticKind.ShortWorkedDays]);
        Assert.Equal(0, statistics[StatisticKind.LongWorkedDays]);
        Assert.Equal(0, statistics[StatisticKind.WorkdaysWithoutEntries]);
        Assert.Equal(1, statistics[StatisticKind.LongestWorkedStreak]);

        var boundary = Uri.EscapeDataString(midnight.ToString("O"));
        var pastOnly = await owner.GetAsync<ReportModel>($"reports?from={from}&to={boundary}&tag=selected");
        Assert.Single(pastOnly.Days);
        Assert.Equal(report.Statistics, pastOnly.Statistics);
    }
    [Fact]
    public async Task PreviousStatisticsUseLocalCalendarDaysAcrossDstAndKeepFiltersAndTenancyAsync()
    {
        using var owner = await fixture.CreateOwnerAsync();
        using var other = await fixture.CreateOwnerAsync();
        var settings = await owner.GetAsync<SettingsModel>("settings");
        await owner.PutAsync<bool>("settings", settings with { TimeZone = "Europe/Prague" });
        var store = fixture.App.Services.GetRequiredService<IDocumentStore>();
        foreach (var tenant in new[] { owner.Owner, other.Owner })
        {
            await using var session = store.LightweightSession(tenant);
            session.Insert(new TimeEntry { Id = "selected", CompatibilityId = 200,
                Start = DateTimeOffset.Parse("2026-03-28T23:00:00Z"), End = DateTimeOffset.Parse("2026-03-29T22:00:00Z"), Tags = ["compare"] });
            session.Insert(new TimeEntry { Id = "ignored", CompatibilityId = 201,
                Start = DateTimeOffset.Parse("2026-03-28T23:00:00Z"), End = DateTimeOffset.Parse("2026-03-29T22:00:00Z"), Tags = ["other"] });
            session.Insert(new TimeEntry { Id = "current", CompatibilityId = 202,
                Start = DateTimeOffset.Parse("2026-03-30T08:00:00Z"), End = DateTimeOffset.Parse("2026-03-30T10:00:00Z"), Tags = ["compare"] });
            await session.SaveChangesAsync();
        }
        var report = await owner.GetAsync<ReportModel>("reports?from=2026-03-30T00:00:00%2B02:00&to=2026-03-31T00:00:00%2B02:00&tag=compare");
        Assert.Equal(7200, report.TotalSeconds);
        Assert.Single(report.Entries);
        Assert.Equal(new DateOnly(2026, 3, 29), report.Comparison!.From);
        Assert.Equal(report.Comparison.From, report.Comparison.Through);
        Assert.Equal(23 * 3600, report.Comparison.Statistics.Single(s => s.Name == StatisticKind.TotalTrackedTime).Value);
        Assert.Equal(1, report.Comparison.Statistics.Single(s => s.Name == StatisticKind.WorkedDays).Value);
    }

}
