namespace WhatHaveIDone.Logic.Reporting;

public static class StatisticsComparisonPeriod
{
    // Use local calendar boundaries: a day crossing DST need not have 24 hours.
    public static (DateTime From, DateTime To)? Previous(DateTime from, DateTime to, DateTime today)
    {
        var end = to < today ? to : today;
        if (end <= from)
        {
            return null;
        }

        if (from.TimeOfDay == TimeSpan.Zero && from.Day == 1 && from.Year > 1)
        {
            if (from.Month == 1 && from.Year < 9999 && to == from.AddYears(1))
            {
                return (from.AddYears(-1), end.AddYears(-1));
            }
            if (from < new DateTime(9999, 12, 1) && to == from.AddMonths(1))
            {
                var previous = from.AddMonths(-1);
                return (previous, end == to ? from
                    : previous.AddDays(Math.Min((end - from).TotalDays, (from - previous).TotalDays)));
            }
        }

        var length = to - from;
        if (from.Ticks < length.Ticks)
        {
            return null;
        }
        return (from - length, end - length);
    }
}
