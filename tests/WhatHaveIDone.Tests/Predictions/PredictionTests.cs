using WhatHaveIDone.Logic.Predictions;

namespace WhatHaveIDone.Tests.Predictions;

public sealed class PredictionTests
{
    private static readonly DateOnly Today = new(2026, 11, 25);
    private static double Capacity(DateOnly day) => WorkCalendarRules.DefaultPercent(day) / 100d;
    private static List<DailyTrackedTime> History() => Enumerable.Range(1, 84)
        .Select(age => Today.AddDays(-age))
        .Select(day => new DailyTrackedTime(day, Capacity(day) * 8 * 3600)).ToList();

    [Theory]
    [InlineData(0, 100, 8)]
    [InlineData(3, 100, 8)]
    [InlineData(10, 100, 10)]
    [InlineData(3, 50, 4)]
    [InlineData(5, 50, 5)]
    [InlineData(2, 0, 2)]
    public void TodayIsToppedUpButRecordedHoursAreNeverRemoved(double recorded, int percent, double expectedToday)
    {
        var history = History();
        history.Add(new DailyTrackedTime(Today, recorded * 3600));
        var result = PredictionService.Calculate(Today, DateTimeOffset.UtcNow, history,
            d => d == Today ? percent / 100d : Capacity(d));
        var prior = history.Where(d => d.Date.Month == Today.Month && d.Date < Today).Sum(d => d.Seconds);
        var futureCapacity = Enumerable.Range(1, 5).Sum(offset => Capacity(Today.AddDays(offset)));
        Assert.Equal(prior + (expectedToday + 8 * futureCapacity) * 3600, result.PredictedSeconds!.Value, 6);
        Assert.True(result.PredictedSeconds >= result.ActualSeconds);
    }

    [Fact]
    public void HalfDaysAndHolidaysAffectTempoWithoutDiscardingRealHours()
    {
        double HalfCapacity(DateOnly day) => Capacity(day) * .5;
        var history = History().Select(d => d with { Seconds = d.Seconds / 2 }).ToList();
        var result = PredictionService.Calculate(Today, DateTimeOffset.UtcNow, history, HalfCapacity);
        Assert.Equal(8 * 3600, result.DailySeconds!.Value, 6);
        var holiday = new DateOnly(2026, 11, 17);
        history.Add(new DailyTrackedTime(holiday, 3600));
        var consolidated = history.GroupBy(d => d.Date).Select(g => new DailyTrackedTime(g.Key, g.Sum(d => d.Seconds))).ToList();
        var extra = PredictionService.Calculate(Today, DateTimeOffset.UtcNow, consolidated, HalfCapacity);
        Assert.True(extra.DailySeconds > result.DailySeconds);
    }

    [Fact]
    public void InsufficientHistoryAndZeroCapacityDoNotProduceANumber()
    {
        var shortHistory = new[] { new DailyTrackedTime(Today.AddDays(-1), 3600) };
        Assert.Null(PredictionService.Calculate(Today, DateTimeOffset.UtcNow, shortHistory, Capacity).PredictedSeconds);
        Assert.Null(PredictionService.Calculate(Today, DateTimeOffset.UtcNow, History(), _ => 0).PredictedSeconds);
    }

    [Theory]
    [InlineData(2024, 3, 29)]
    [InlineData(2024, 4, 1)]
    [InlineData(2025, 4, 18)]
    [InlineData(2025, 4, 21)]
    [InlineData(2026, 4, 3)]
    [InlineData(2026, 4, 6)]
    [InlineData(2026, 11, 17)]
    public void CzechHolidaysAreOff(int year, int month, int day)
    {
        Assert.Equal(0, WorkCalendarRules.DefaultPercent(new DateOnly(year, month, day)));
        Assert.NotNull(WorkCalendarRules.Holiday(new DateOnly(year, month, day)));
    }

    [Theory]
    [InlineData(2026, 3, 29, 23)]
    [InlineData(2026, 10, 25, 25)]
    public void LocalDayBoundariesRespectDst(int year, int month, int day, int hours)
    {
        var date = new DateOnly(year, month, day);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");
        Assert.Equal(hours, (WorkCalendarRules.DayStart(date.AddDays(1), zone) - WorkCalendarRules.DayStart(date, zone)).TotalHours);
    }
}
