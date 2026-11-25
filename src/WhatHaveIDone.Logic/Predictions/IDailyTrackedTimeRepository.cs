namespace WhatHaveIDone.Logic.Predictions;

public interface IDailyTrackedTimeRepository
{
    Task<IReadOnlyList<DailyTrackedTime>> ListAsync(DailyTrackedTimeSpecification specification, CancellationToken cancellationToken);
}
