using System.Globalization;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Logic.TimeEntries;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Reporting;

public sealed class ReportQuery(IReadOnlyRepository<TimeEntry> entries, IReadOnlyRepository<UserProfile> profiles,
    TimeProvider clock, IStatisticsProvider statisticsProvider)
{
    public async Task<ReportModel> RunAsync(DateTimeOffset from, DateTimeOffset to, string? project, string? tag, string? text, CancellationToken cancellationToken)
    {
        if (to <= from) throw new AppException("InvalidInterval");
        var profile = await profiles.RequireAsync(new UserProfileSpecification(), cancellationToken);
        var selected = (await entries.ListAsync(new TimeEntriesSpecification(from, to, project, tag, text), cancellationToken)).ToList();
        var now = clock.GetUtcNow();
        var projects = selected.GroupBy(e => e.ProjectId ?? "none").Select(g => new ReportBucket(g.Key, g.Sum(e => Seconds(e, from, to, now)))).OrderByDescending(g => g.Seconds).ToList();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(profile.TimeZone);
        var days = DailyTotals(selected, from, to, now, zone, cancellationToken);
        var localFrom = TimeZoneInfo.ConvertTime(from, zone).DateTime;
        var localTo = TimeZoneInfo.ConvertTime(to, zone).DateTime;
        var today = TimeZoneInfo.ConvertTime(now, zone).Date;
        var statistics = statisticsProvider.Calculate(days,
            DateOnly.FromDateTime(localFrom), DateOnly.FromDateTime(localTo.AddTicks(-1)),
            DateOnly.FromDateTime(today), cancellationToken);
        StatisticsComparison? comparison = null;
        if (StatisticsComparisonPeriod.Previous(localFrom, localTo, today) is { } previous)
        {
            var previousFrom = DayStart(previous.From, zone);
            var previousTo = DayStart(previous.To, zone);
            var previousEntries = await entries.ListAsync(
                new TimeEntriesSpecification(previousFrom, previousTo, project, tag, text), cancellationToken);
            var previousDays = DailyTotals(previousEntries, previousFrom, previousTo, now, zone, cancellationToken);
            var previousFirst = DateOnly.FromDateTime(previous.From);
            var previousLast = DateOnly.FromDateTime(previous.To.AddTicks(-1));
            comparison = new StatisticsComparison(previousFirst, previousLast,
                statisticsProvider.Calculate(previousDays, previousFirst, previousLast, DateOnly.FromDateTime(today), cancellationToken));
        }
        return new ReportModel(now, projects.Sum(g => g.Seconds), projects,
            days.OrderBy(d => d.Key).Select(d => new ReportBucket(d.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), d.Value)).ToList(),
            selected, statistics, comparison);
    }

    private static Dictionary<DateOnly, double> DailyTotals(IEnumerable<TimeEntryModel> selected,
        DateTimeOffset from, DateTimeOffset to, DateTimeOffset now, TimeZoneInfo zone, CancellationToken cancellationToken)
    {
        var days = new Dictionary<DateOnly, double>();
        foreach (var entry in selected)
        {
            var clippedStart = entry.Start > from ? entry.Start : from;
            var clippedEnd = (entry.End ?? now) < to ? entry.End ?? now : to;
            for (var day = TimeZoneInfo.ConvertTime(clippedStart, zone).Date; DayStart(day, zone) < clippedEnd; day = day.AddDays(1))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var start = DayStart(day, zone); var end = DayStart(day.AddDays(1), zone);
                var seconds = Seconds(entry, start > from ? start : from, end < to ? end : to, now);
                if (seconds <= 0) continue;
                var key = DateOnly.FromDateTime(day); days[key] = days.GetValueOrDefault(key) + seconds;
            }
        }
        return days;
    }

    public static double Seconds(TimeEntryModel entry, DateTimeOffset from, DateTimeOffset to, DateTimeOffset now)
        => to <= from ? 0 : Math.Max(0, ((entry.End ?? now) < to ? entry.End ?? now : to).Subtract(entry.Start > from ? entry.Start : from).TotalSeconds);
    private static DateTimeOffset DayStart(DateTime local, TimeZoneInfo zone)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        if (zone.IsAmbiguousTime(local)) return new DateTimeOffset(local, zone.GetAmbiguousTimeOffsets(local).Max()).ToUniversalTime();
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }
}
