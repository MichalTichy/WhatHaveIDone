using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Predictions;

namespace WhatHaveIDone.Logic.Reporting;

public sealed class StatisticsProvider : IStatisticsProvider
{
    public IReadOnlyList<DataStatistics> Calculate(IReadOnlyDictionary<DateOnly, double> days,
        DateOnly from, DateOnly through, DateOnly today, CancellationToken cancellationToken)
    {
        var durations = new List<double>();
        var shortDays = 0;
        var shortDaysOff = 0;
        var longDays = 0;
        var emptyWorkdays = 0;
        var offDaySeconds = 0d;
        var workedDaysOff = 0;
        var streak = 0;
        var longestStreak = 0;
        var lastDayNumber = Math.Min(through.DayNumber, today.DayNumber - 1);

        for (var dayNumber = from.DayNumber; dayNumber <= lastDayNumber; dayNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var day = DateOnly.FromDayNumber(dayNumber);
            var seconds = days.GetValueOrDefault(day);
            var isOff = WorkCalendarRules.DefaultPercent(day) == 0;
            if (seconds > 0)
            {
                durations.Add(seconds);
                streak++;
                longestStreak = Math.Max(longestStreak, streak);
                if (seconds < 2 * 3600)
                {
                    shortDays++;
                    if (isOff)
                    {
                        shortDaysOff++;
                    }
                }
                if (seconds >= 8 * 3600)
                {
                    longDays++;
                }
                if (isOff)
                {
                    offDaySeconds += seconds;
                    workedDaysOff++;
                }
            }
            else
            {
                streak = 0;
                if (!isOff)
                {
                    emptyWorkdays++;
                }
            }
        }

        durations.Sort();
        var total = durations.Sum();
        double? average = durations.Count > 0 ? total / durations.Count : null;
        var workedWorkdays = durations.Count - workedDaysOff;
        double? workdayAverage = workedWorkdays > 0 ? (total - offDaySeconds) / workedWorkdays : null;
        double? dayOffAverage = workedDaysOff > 0 ? offDaySeconds / workedDaysOff : null;
        double? median = durations.Count > 0
            ? (durations[(durations.Count - 1) / 2] + durations[durations.Count / 2]) / 2
            : null;

        return
        [
            new(StatisticKind.TotalTrackedTime, total, StatisticUnit.Seconds),
            new(StatisticKind.WorkedDays, durations.Count, StatisticUnit.Days),
            new(StatisticKind.AverageWorkedDay, average, StatisticUnit.Seconds),
            new(StatisticKind.AverageWorkedWorkday, workdayAverage, StatisticUnit.Seconds),
            new(StatisticKind.AverageWorkedDayOff, dayOffAverage, StatisticUnit.Seconds),
            new(StatisticKind.ShortWorkedDays, shortDays, StatisticUnit.Days),
            new(StatisticKind.ShortWorkedDaysOff, shortDaysOff, StatisticUnit.Days),
            new(StatisticKind.LongWorkedDays, longDays, StatisticUnit.Days),
            new(StatisticKind.WorkdaysWithoutEntries, emptyWorkdays, StatisticUnit.Days),
            new(StatisticKind.WorkdaysWithUnderTwoHours, emptyWorkdays + shortDays - shortDaysOff, StatisticUnit.Days),
            new(StatisticKind.WorkedDaysOff, workedDaysOff, StatisticUnit.Days),
            new(StatisticKind.OffDayTime, offDaySeconds, StatisticUnit.Seconds),
            new(StatisticKind.OffDayTimePercentage, total > 0 ? offDaySeconds / total * 100 : 0, StatisticUnit.Percentage),
            new(StatisticKind.MedianWorkedDay, median, StatisticUnit.Seconds),
            new(StatisticKind.LongestWorkedStreak, longestStreak, StatisticUnit.Days)
        ];
    }
}
