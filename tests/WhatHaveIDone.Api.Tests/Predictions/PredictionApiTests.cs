using System.Net;
using System.Net.Http.Json;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Predictions;
using WhatHaveIDone.Logic.Tenancy;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Api.Tests;

[Collection("postgres")]
public sealed class PredictionApiTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task SettingsAndCalendarsAreTenantScopedVersionedAndValidatedAsync()
    {
        using var alice = await fixture.CreateOwnerAsync();
        using var bob = await fixture.CreateOwnerAsync();
        using var anonymous = fixture.App.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/prediction")).StatusCode);
        var settings = await alice.GetAsync<SettingsModel>("settings");
        Assert.False(settings.PredictionEnabled);
        var enabled = await alice.PutAsync<PredictionSettingsModel>("prediction/settings", new PredictionSettingsModel(true, settings.Version));
        Assert.NotEqual(settings.Version, enabled.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await alice.Http.PutAsJsonAsync("/api/v1/prediction/settings", new PredictionSettingsModel(false, settings.Version))).StatusCode);
        Assert.False((await bob.GetAsync<MonthlyPredictionModel>("prediction")).Enabled);
        var freshSettings = await alice.GetAsync<SettingsModel>("settings");
        await alice.PutAsync<bool>("settings", freshSettings with { Language = "en" });
        Assert.True((await alice.GetAsync<SettingsModel>("settings")).PredictionEnabled);

        var month = await alice.GetAsync<WorkCalendarModel>("prediction/calendar/2026/11");
        var date = new DateOnly(2026, 11, 17);
        Assert.Equal(0, month.Days.Single(d => d.Date == date).Percent);
        await alice.PutAsync<bool>("prediction/calendar/day", new SaveWorkdayRequest(date, 50, month.Version));
        var changed = await alice.GetAsync<WorkCalendarModel>("prediction/calendar/2026/11");
        Assert.Equal(50, changed.Days.Single(d => d.Date == date).Percent);
        Assert.True(changed.Days.Single(d => d.Date == date).IsOverride);
        Assert.Equal(0, (await bob.GetAsync<WorkCalendarModel>("prediction/calendar/2026/11")).Days.Single(d => d.Date == date).Percent);
        Assert.Equal(HttpStatusCode.Conflict, (await alice.Http.PutAsJsonAsync("/api/v1/prediction/calendar/day", new SaveWorkdayRequest(date, 75, month.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.Http.PutAsJsonAsync("/api/v1/prediction/calendar/day", new SaveWorkdayRequest(date, 33, changed.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.Http.GetAsync("/api/v1/prediction/calendar/2026/13")).StatusCode);
        await alice.PutAsync<bool>("prediction/calendar/day", new SaveWorkdayRequest(date, null, changed.Version));
        var reset = (await alice.GetAsync<WorkCalendarModel>("prediction/calendar/2026/11")).Days.Single(d => d.Date == date);
        Assert.Equal(0, reset.Percent);
        Assert.False(reset.IsOverride);
    }

    [Fact]
    public async Task ConcurrentFirstCalendarWritesCannotSilentlyOverwriteAsync()
    {
        using var owner = await fixture.CreateOwnerAsync();
        var requests = new[] { 5, 6 }.Select(day => owner.Http.PutAsJsonAsync("/api/v1/prediction/calendar/day",
            new SaveWorkdayRequest(new DateOnly(2026, 11, day), 50, Guid.Empty)));
        var results = await Task.WhenAll(requests);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in results)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task AggregateSplitsMidnightAndDstClipsRunningEntriesAndExcludesOtherOwnersAsync()
    {
        using var owner = await fixture.CreateOwnerAsync();
        using var other = await fixture.CreateOwnerAsync();
        var store = fixture.App.Services.GetRequiredService<IDocumentStore>();
        await using (var session = store.LightweightSession(owner.Owner))
        {
            session.Insert(Entry(1, "2026-10-24T23:00:00+02:00", "2026-10-25T03:00:00+01:00"));
            session.Insert(Entry(2, "2026-10-25T02:15:00+02:00", "2026-10-25T02:15:00+01:00"));
            session.Insert(Entry(3, "2026-10-26T11:00:00Z", null));
            session.Insert(Entry(4, "2026-10-27T11:00:00Z", "2026-10-27T12:00:00Z"));
            await session.SaveChangesAsync();
        }
        await using (var session = store.LightweightSession(other.Owner))
        {
            session.Insert(Entry(1, "2026-10-25T09:00:00Z", "2026-10-25T17:00:00Z"));
            await session.SaveChangesAsync();
        }
        await using var scope = fixture.App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentTenancyProvider>().SetBackgroundUser(owner.Owner);
        var repository = scope.ServiceProvider.GetRequiredService<IDailyTrackedTimeRepository>();
        var daily = await repository.ListAsync(new DailyTrackedTimeSpecification(new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 26),
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague"), DateTimeOffset.Parse("2026-10-26T12:00:00Z")), CancellationToken.None);
        Assert.Equal(3600, daily.Single(d => d.Date.Day == 24).Seconds);
        Assert.Equal(5 * 3600, daily.Single(d => d.Date.Day == 25).Seconds);
        Assert.Equal(3600, daily.Single(d => d.Date.Day == 26).Seconds);
        Assert.Equal(3, daily.Count);
    }

    [Fact]
    public async Task AggregateHandlesShortSpringDayAndExactMidnightAsync()
    {
        using var owner = await fixture.CreateOwnerAsync();
        var store = fixture.App.Services.GetRequiredService<IDocumentStore>();
        await using (var session = store.LightweightSession(owner.Owner))
        {
            session.Insert(Entry(1, "2026-03-28T23:00:00+01:00", "2026-03-29T04:00:00+02:00"));
            session.Insert(Entry(2, "2026-03-29T00:00:00+01:00", "2026-03-30T00:00:00+02:00"));
            session.Insert(Entry(3, "2026-03-29T01:30:00+01:00", "2026-03-29T03:30:00+02:00"));
            await session.SaveChangesAsync();
        }
        await using var scope = fixture.App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentTenancyProvider>().SetBackgroundUser(owner.Owner);
        var daily = await scope.ServiceProvider.GetRequiredService<IDailyTrackedTimeRepository>().ListAsync(
            new DailyTrackedTimeSpecification(new DateOnly(2026, 3, 28), new DateOnly(2026, 3, 30),
                TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague"), DateTimeOffset.Parse("2026-03-30T12:00:00Z")), CancellationToken.None);
        Assert.Equal(3600, daily.Single(d => d.Date.Day == 28).Seconds);
        Assert.Equal(27 * 3600, daily.Single(d => d.Date.Day == 29).Seconds);
        Assert.DoesNotContain(daily, d => d.Date.Day == 30);
    }

    [Fact]
    public async Task PredictionUsesDatabaseAggregatesAndCalendarCapacityAsync()
    {
        using var owner = await fixture.CreateOwnerAsync();
        var settings = await owner.GetAsync<SettingsModel>("settings");
        await owner.PutAsync<PredictionSettingsModel>("prediction/settings", new PredictionSettingsModel(true, settings.Version));
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(fixture.Clock.GetUtcNow(), "Europe/Prague").DateTime);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");
        var store = fixture.App.Services.GetRequiredService<IDocumentStore>();
        await using (var session = store.LightweightSession(owner.Owner))
        {
            for (var age = 1; age <= 84; age++)
            {
                var day = today.AddDays(-age);
                if (WorkCalendarRules.DefaultPercent(day) == 0)
                {
                    continue;
                }
                var start = WorkCalendarRules.DayStart(day, zone).AddHours(8);
                session.Insert(new TimeEntry { Id = Guid.NewGuid().ToString("N"), CompatibilityId = age, Start = start, End = start.AddHours(8) });
            }
            await session.SaveChangesAsync();
        }
        var prediction = await owner.GetAsync<MonthlyPredictionModel>("prediction");
        Assert.Equal(8 * 3600, prediction.DailySeconds!.Value, 5);
        Assert.True(prediction.PredictedSeconds >= prediction.ActualSeconds);
        var month = await owner.GetAsync<WorkCalendarModel>($"prediction/calendar/{today.Year}/{today.Month}");
        var future = month.Days.FirstOrDefault(d => d.Date >= today && d.Percent == 100);
        if (future is not null)
        {
            await owner.PutAsync<bool>("prediction/calendar/day", new SaveWorkdayRequest(future.Date, 50, month.Version));
            var changed = await owner.GetAsync<MonthlyPredictionModel>("prediction");
            Assert.Equal(prediction.PredictedSeconds.Value - 4 * 3600, changed.PredictedSeconds!.Value, 5);
        }
    }

    private static TimeEntry Entry(long compatibilityId, string start, string? end) => new()
    {
        Id = Guid.NewGuid().ToString("N"), CompatibilityId = compatibilityId,
        Start = DateTimeOffset.Parse(start), End = end is null ? null : DateTimeOffset.Parse(end)
    };
}
