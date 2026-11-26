using WhatHaveIDone.API.Models;

namespace WhatHaveIDone.Logic.Reporting;

public interface IStatisticsProvider
{
    IReadOnlyList<DataStatistics> Calculate(IReadOnlyDictionary<DateOnly, double> days,
        DateOnly from, DateOnly through, DateOnly today, CancellationToken cancellationToken);
}
