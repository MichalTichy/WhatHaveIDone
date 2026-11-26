using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Reporting;

namespace WhatHaveIDone.Tests.Reporting;

public sealed class StatisticsTests
{
    private readonly IStatisticsProvider provider = new StatisticsProvider();

    [Fact]
    public void EveryMetricExcludesTodayFutureAndDatesOutsideTheSelection()
    {
        var from = new DateOnly(2026, 11, 16);
        var today = new DateOnly(2026, 11, 23);
        var days = new Dictionary<DateOnly, double>
        {
            [from.AddDays(-1)] = 100 * 3600,
            [from] = 3600,
            [from.AddDays(1)] = 1800, // Czech holiday, Tuesday.
            [from.AddDays(2)] = 2 * 3600,
            [from.AddDays(4)] = 8 * 3600,
            [from.AddDays(5)] = 3600, // Saturday.
            [today] = 20 * 3600,
            [today.AddDays(1)] = 3600
        };

        var result = Calculate(days, from, today.AddDays(10), today);

        Assert.Equal(12.5 * 3600 / 5, result[StatisticKind.AverageWorkedDay]);
        Assert.Equal(12.5 * 3600, result[StatisticKind.TotalTrackedTime]);
        Assert.Equal(5, result[StatisticKind.WorkedDays]);
        Assert.Equal(2, result[StatisticKind.WorkedDaysOff]);
        Assert.Equal(11 * 3600 / 3, result[StatisticKind.AverageWorkedWorkday]);
        Assert.Equal(0.75 * 3600, result[StatisticKind.AverageWorkedDayOff]);
        Assert.Equal(3600, result[StatisticKind.MedianWorkedDay]);
        Assert.Equal(3, result[StatisticKind.ShortWorkedDays]);
        Assert.Equal(2, result[StatisticKind.ShortWorkedDaysOff]);
        Assert.Equal(1, result[StatisticKind.LongWorkedDays]);
        Assert.Equal(1, result[StatisticKind.WorkdaysWithoutEntries]);
        Assert.Equal(2, result[StatisticKind.WorkdaysWithUnderTwoHours]);
        Assert.Equal(1.5 * 3600, result[StatisticKind.OffDayTime]);
        Assert.Equal(12, result[StatisticKind.OffDayTimePercentage]);
        Assert.Equal(3, result[StatisticKind.LongestWorkedStreak]);
    }

    [Fact]
    public void EmptyPastPeriodCountsOnlyOrdinaryWorkdays()
    {
        var from = new DateOnly(2026, 4, 3); // Good Friday, weekend, Easter Monday, Tuesday.
        var result = Calculate(new Dictionary<DateOnly, double>(), from, from.AddDays(4), from.AddDays(10));

        Assert.Equal(1, result[StatisticKind.WorkdaysWithoutEntries]);
        Assert.Equal(1, result[StatisticKind.WorkdaysWithUnderTwoHours]);
        Assert.Null(result[StatisticKind.AverageWorkedDay]);
        Assert.Null(result[StatisticKind.AverageWorkedWorkday]);
        Assert.Null(result[StatisticKind.AverageWorkedDayOff]);
        Assert.Null(result[StatisticKind.MedianWorkedDay]);
        Assert.All(result.Where(s => s.Key is not (StatisticKind.WorkdaysWithoutEntries or StatisticKind.WorkdaysWithUnderTwoHours
            or StatisticKind.AverageWorkedDay or StatisticKind.AverageWorkedWorkday
            or StatisticKind.AverageWorkedDayOff or StatisticKind.MedianWorkedDay)), s => Assert.Equal(0, s.Value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SelectionStartingTodayOrLaterHasNoStatisticsFromPastDays(int offset)
    {
        var today = new DateOnly(2026, 11, 23);
        var result = Calculate(new Dictionary<DateOnly, double> { [today] = 9 * 3600 },
            today.AddDays(offset), today.AddDays(20), today);

        Assert.Null(result[StatisticKind.AverageWorkedDay]);
        Assert.Null(result[StatisticKind.AverageWorkedWorkday]);
        Assert.Null(result[StatisticKind.AverageWorkedDayOff]);
        Assert.Null(result[StatisticKind.MedianWorkedDay]);
        Assert.All(result.Where(s => s.Value.HasValue), s => Assert.Equal(0, s.Value));
    }

    [Fact]
    public void EvenMedianAndStreakUseOnlySelectedDaysAndZeroDoesNotCountAsWork()
    {
        var from = new DateOnly(2026, 11, 19);
        var days = new Dictionary<DateOnly, double>
        {
            [from.AddDays(-1)] = 10 * 3600,
            [from] = 2 * 3600,
            [from.AddDays(1)] = 8 * 3600,
            [from.AddDays(2)] = 0,
            [from.AddDays(3)] = 10 * 3600
        };
        var result = Calculate(days, from, from.AddDays(2), from.AddDays(10));

        Assert.Equal(5 * 3600, result[StatisticKind.MedianWorkedDay]);
        Assert.Equal(5 * 3600, result[StatisticKind.AverageWorkedDay]);
        Assert.Equal(0, result[StatisticKind.ShortWorkedDays]);
        Assert.Equal(0, result[StatisticKind.WorkdaysWithUnderTwoHours]);
        Assert.Equal(1, result[StatisticKind.LongWorkedDays]);
        Assert.Equal(2, result[StatisticKind.LongestWorkedStreak]);
    }

    [Fact]
    public void HolidayOnWeekendIsCountedOnce()
    {
        var holiday = new DateOnly(2026, 7, 5); // Sunday.
        var result = Calculate(new Dictionary<DateOnly, double> { [holiday] = 3600 },
            holiday, holiday, holiday.AddDays(1));

        Assert.Equal(1, result[StatisticKind.ShortWorkedDaysOff]);
        Assert.Equal(3600, result[StatisticKind.OffDayTime]);
        Assert.Equal(100, result[StatisticKind.OffDayTimePercentage]);
    }

    [Fact]
    public void CalculationObservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var day = new DateOnly(2026, 11, 23);
        Assert.Throws<OperationCanceledException>(() => provider.Calculate(new Dictionary<DateOnly, double>(), day, day, day.AddDays(1), cancellation.Token));
    }

    [Theory]
    [InlineData(16, true)] // Monday.
    [InlineData(17, false)] // Czech holiday on Tuesday.
    [InlineData(21, false)] // Saturday.
    public void SubAveragesCountOnlyDaysWithWorkAndLeaveAbsentGroupUndefined(int dayOfMonth, bool isWorkday)
    {
        var day = new DateOnly(2026, 11, dayOfMonth);
        var days = new Dictionary<DateOnly, double> { [day] = 3 * 3600, [day.AddDays(1)] = 0 };
        var result = Calculate(days, day, day.AddDays(1), day.AddDays(2));

        Assert.Equal(3 * 3600, result[StatisticKind.AverageWorkedDay]);
        Assert.Equal(isWorkday ? 3 * 3600 : null, result[StatisticKind.AverageWorkedWorkday]);
        Assert.Equal(isWorkday ? null : 3 * 3600, result[StatisticKind.AverageWorkedDayOff]);
    }

    private Dictionary<StatisticKind, double?> Calculate(IReadOnlyDictionary<DateOnly, double> days,
        DateOnly from, DateOnly through, DateOnly today) =>
        provider.Calculate(days, from, through, today, CancellationToken.None).ToDictionary(s => s.Name, s => s.Value);
}
