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
}
