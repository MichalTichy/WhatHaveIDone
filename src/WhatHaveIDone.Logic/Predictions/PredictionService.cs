using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Predictions;

public sealed class PredictionService(IReadOnlyRepository<UserProfile> profiles, IReadOnlyRepository<WorkCalendarMonth> calendars,
    IDailyTrackedTimeRepository dailyTime, TimeProvider clock)
{
    public async Task<WorkCalendarModel> GetCalendarAsync(int year, int month, CancellationToken cancellationToken)
    {
        WorkCalendarRules.ValidateMonth(year, month);
        var first = new DateOnly(year, month, 1);
        var calendar = await calendars.GetByIdAsync(WorkCalendarRules.MonthId(first), cancellationToken);
        var days = Enumerable.Range(1, DateTime.DaysInMonth(year, month)).Select(day =>
        {
            var date = first.AddDays(day - 1);
            var defaultPercent = WorkCalendarRules.DefaultPercent(date);
            var overridden = calendar?.Overrides.ContainsKey(day) == true;
            return new WorkdayModel(date, overridden ? calendar!.Overrides[day] : defaultPercent, defaultPercent, overridden, WorkCalendarRules.Holiday(date));
        }).ToArray();
        return new WorkCalendarModel(year, month, calendar?.Version ?? Guid.Empty, days);
    }

    public async Task<MonthlyPredictionModel> GetMonthlyAsync(CancellationToken cancellationToken)
    {
        var profile = await profiles.RequireAsync(new UserProfileSpecification(), cancellationToken);
        var now = clock.GetUtcNow();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(profile.TimeZone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var month = new DateOnly(today.Year, today.Month, 1);
        if (!profile.PredictionEnabled)
        {
            return new MonthlyPredictionModel(month, now, false, 0, null, null);
        }
        var from = today.AddDays(-84);
        var end = month.AddMonths(1);
        var calendarIds = new List<string>();
        for (var date = new DateOnly(from.Year, from.Month, 1); date < end; date = date.AddMonths(1))
        {
            calendarIds.Add(WorkCalendarRules.MonthId(date));
        }
        var overrides = (await calendars.GetByIdsAsync(calendarIds, cancellationToken)).ToDictionary(c => c.Id);
        var daily = await dailyTime.ListAsync(new DailyTrackedTimeSpecification(from, today, zone, now), cancellationToken);
        return Calculate(today, now, daily, date =>
        {
            var percent = overrides.TryGetValue(WorkCalendarRules.MonthId(date), out var calendar)
                && calendar.Overrides.TryGetValue(date.Day, out var custom) ? custom : WorkCalendarRules.DefaultPercent(date);
            return percent / 100d;
        });
    }

    public static MonthlyPredictionModel Calculate(DateOnly today, DateTimeOffset now, IReadOnlyList<DailyTrackedTime> daily, Func<DateOnly, double> capacity)
    {
        var month = new DateOnly(today.Year, today.Month, 1);
        var seconds = daily.ToDictionary(d => d.Date, d => d.Seconds);
        var actual = daily.Where(d => d.Date >= month && d.Date <= today).Sum(d => d.Seconds);
        var historical = daily.Where(d => d.Date < today && d.Seconds > 0).ToList();
        // Avoid presenting a confident full-month projection from a newly started account.
        if (historical.Count == 0 || historical.Min(d => d.Date) > today.AddDays(-14))
        {
            return new MonthlyPredictionModel(month, now, true, actual, null, null);
        }
        double weightedSeconds = 0, weightedCapacity = 0;
        for (var date = today.AddDays(-84); date < today; date = date.AddDays(1))
        {
            var weight = Math.Pow(2, -(today.DayNumber - date.DayNumber - 1) / 28d);
            weightedSeconds += seconds.GetValueOrDefault(date) * weight;
            weightedCapacity += capacity(date) * weight;
        }
        if (weightedCapacity <= 0)
        {
            return new MonthlyPredictionModel(month, now, true, actual, null, null);
        }
        var rate = weightedSeconds / weightedCapacity;
        var remaining = Math.Max(0, rate * capacity(today) - seconds.GetValueOrDefault(today));
        for (var date = today.AddDays(1); date < month.AddMonths(1); date = date.AddDays(1))
        {
            remaining += rate * capacity(date);
        }
        return new MonthlyPredictionModel(month, now, true, actual, actual + remaining, rate);
    }
}
