using WhatHaveIDone.Logic.Reporting;

namespace WhatHaveIDone.Tests.Reporting;

public sealed class StatisticsComparisonPeriodTests
{
    [Theory]
    [InlineData("2026-03-01", "2026-04-01", "2026-05-01", "2026-02-01", "2026-03-01")]
    [InlineData("2026-11-01", "2026-12-01", "2026-12-10", "2026-10-01", "2026-11-01")]
    [InlineData("2026-02-01", "2026-03-01", "2026-04-01", "2026-01-01", "2026-02-01")]
    [InlineData("2026-11-01", "2026-12-01", "2026-11-26", "2026-10-01", "2026-10-26")]
    [InlineData("2026-03-01", "2026-04-01", "2026-03-31", "2026-02-01", "2026-03-01")]
    [InlineData("2024-01-01", "2025-01-01", "2025-02-01", "2023-01-01", "2024-01-01")]
    [InlineData("2024-01-01", "2025-01-01", "2024-03-01", "2023-01-01", "2023-03-01")]
    [InlineData("2026-11-16", "2026-11-23", "2026-11-26", "2026-11-09", "2026-11-16")]
    [InlineData("2026-11-16", "2026-11-23", "2026-11-20", "2026-11-09", "2026-11-13")]
    [InlineData("2026-10-25", "2026-10-26", "2026-11-26", "2026-10-24", "2026-10-25")]
    public void ComparisonUsesCalendarPeriodsAndOnlyElapsedDays(string from, string to, string today, string expectedFrom, string expectedTo)
    {
        var result = StatisticsComparisonPeriod.Previous(DateTime.Parse(from), DateTime.Parse(to), DateTime.Parse(today));
        Assert.NotNull(result);
        Assert.Equal(DateTime.Parse(expectedFrom), result.Value.From);
        Assert.Equal(DateTime.Parse(expectedTo), result.Value.To);
    }

    [Theory]
    [InlineData("2026-11-26")]
    [InlineData("2026-11-27")]
    public void NoComparisonWithoutCompletedDays(string start)
    {
        var from = DateTime.Parse(start);
        Assert.Null(StatisticsComparisonPeriod.Previous(from, from.AddDays(1), new DateTime(2026, 11, 26)));
    }
}
