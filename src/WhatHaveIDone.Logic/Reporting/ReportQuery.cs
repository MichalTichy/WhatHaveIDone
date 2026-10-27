using System.Globalization;
using Shared.Infrastructure.CurrentUserProvider;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Contracts;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.TimeEntries;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Reporting;

public sealed class ReportQuery(ICurrentUserIdProvider actor, IReadOnlyRepository<TimeEntry> entries, IReadOnlyRepository<UserProfile> profiles, TimeProvider clock)
{
    public async Task<ReportModel> RunAsync(DateTimeOffset from, DateTimeOffset to, string? project, string? tag, string? text, CancellationToken cancellationToken)
    {
        if (to <= from) throw new AppException("InvalidInterval");
        var owner = await actor.GetCurrentUserIdRequiredAsync();
        var profile = await profiles.RequireAsync(owner, owner, cancellationToken);
        var selected = (await entries.ListAsync(new TimeEntriesSpecification(owner, from, to, project, tag, text), cancellationToken)).ToList();
        var now = clock.GetUtcNow();
        var projects = selected.GroupBy(e => e.ProjectId ?? "none").Select(g => new ReportBucket(g.Key, g.Sum(e => Seconds(e, from, to, now)))).OrderByDescending(g => g.Seconds).ToList();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(profile.TimeZone);
        var days = new Dictionary<string,double>();
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
                var key = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); days[key] = days.GetValueOrDefault(key) + seconds;
            }
        }
        return new ReportModel(now, projects.Sum(g => g.Seconds), projects, days.OrderBy(d=>d.Key).Select(d=>new ReportBucket(d.Key,d.Value)).ToList(), selected);
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
