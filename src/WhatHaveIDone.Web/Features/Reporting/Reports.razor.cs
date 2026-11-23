using System.Globalization;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Web.Resources;
using WhatHaveIDone.Web.Services;

namespace WhatHaveIDone.Web.Features.Reporting;

public partial class Reports
{
    private DateTime startDate, endDate;
    private DateTimeOffset appliedFrom, appliedTo;
    private DateTime activityFrom, activityThrough;
    private Dictionary<DateTime, double> activity = [];
    private double busiestDay;
    private string? projectPie;

    [Inject]
    private TimeProvider Clock { get; set; } = null!;
    private string project = "", tag = "", text = "";
    private bool detail;
    private ReportModel? report;
    private string CsvUrl { get; set; } = "";

    private IReadOnlyList<SelectOption<string>> ProjectOptions =>
    [
        new("", Strings.AllProjects),
        new("none", Strings.NoProject),
        .. User.ProjectsByUsage.Select(p => new SelectOption<string>(p.Id, p.Name + " · " + User.ClientName(p.ClientId)))
    ];

    private IReadOnlyList<SelectOption<string>> TagOptions =>
    [
        new("", Strings.AllTags),
        .. User.Tags.Select(name => new SelectOption<string>(name, name))
    ];

    private void SelectTag(string? value) => tag = value ?? "";
    private void SelectProject(string? value) => project = value ?? "";

    private IEnumerable<(string Label, DateTime From, DateTime Through)> Periods()
    {
        var today = UserTime.Local(Clock.GetUtcNow(), User.Settings.TimeZone).Date;
        var week = today.AddDays(-WeekdayColumn(today.DayOfWeek));
        var month = new DateTime(today.Year, today.Month, 1);
        var year = new DateTime(today.Year, 1, 1);

        yield return (Strings.Today, today, today);
        yield return (Strings.ThisWeek, week, week.AddDays(6));
        yield return (Strings.LastWeek, week.AddDays(-7), week.AddDays(-1));
        yield return (Strings.ThisMonth, month, month.AddMonths(1).AddDays(-1));
        yield return (Strings.LastMonth, month.AddMonths(-1), month.AddDays(-1));
        yield return (Strings.ThisYear, year, year.AddYears(1).AddDays(-1));
        yield return (Strings.LastYear, year.AddYears(-1), year.AddDays(-1));
    }

    private int WeekdayColumn(DayOfWeek day) => ((int)day - User.Settings.FirstDayOfWeek + 7) % 7;

    private IEnumerable<string> WeekdayLabels() => Enumerable.Range(0, 7).Select(offset =>
        CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName((DayOfWeek)((User.Settings.FirstDayOfWeek + offset) % 7)));

    protected override Task OnInitializedAsync()
    {
        var today = UserTime.Local(Clock.GetUtcNow(), User.Settings.TimeZone).Date;
        var month = new DateTime(today.Year, today.Month, 1);
        return SelectPeriodAsync(month, month.AddMonths(1).AddDays(-1));
    }

    private Task SelectPeriodAsync(DateTime from, DateTime through)
    {
        startDate = from;
        endDate = through;
        return LoadAsync();
    }

    private Task LoadAsync()
    {
        var firstDay = startDate.Date;
        var lastDay = endDate.Date;
        var from = UserTime.Instant(firstDay, User.Settings.TimeZone);
        var to = UserTime.Instant(lastDay.AddDays(1), User.Settings.TimeZone);
        var filters = (Project: project, Tag: tag, Text: text);
        return ExecuteLoadAsync(ct => Api.GetReportAsync(from, to, filters.Project, filters.Tag, filters.Text, ct), selected =>
        {
            report = selected;
            CsvUrl = Api.GetReportCsvUrl(from, to, filters.Project, filters.Tag, filters.Text);
            appliedFrom = from;
            appliedTo = to;
            activityFrom = firstDay;
            activityThrough = lastDay;
            activity = selected.Days.ToDictionary(
                day => DateTime.ParseExact(day.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                day => day.Seconds);
            busiestDay = activity.Values.DefaultIfEmpty().Max();
            projectPie = ProjectPie(selected);
        });
    }

    private Task FilterProjectAsync(string? value)
    {
        project = value ?? "none";
        return LoadAsync();
    }

    private string? ProjectPie(ReportModel selected)
    {
        if (selected.TotalSeconds <= 0)
        {
            return null;
        }

        var stops = new List<string>();
        var start = 0d;
        foreach (var bucket in selected.Projects.Where(bucket => bucket.Seconds > 0))
        {
            var end = start + bucket.Seconds / selected.TotalSeconds * 100;
            stops.Add(FormattableString.Invariant($"{User.ProjectColor(bucket.Key)} {start:F5}% {end:F5}%"));
            start = end;
        }

        return $"conic-gradient({string.Join(", ", stops)})";
    }

    private IEnumerable<DateTime> ActivityMonths()
    {
        var month = new DateTime(activityFrom.Year, activityFrom.Month, 1);
        while (month <= activityThrough)
        {
            yield return month;
            month = month.AddMonths(1);
        }
    }

    private IEnumerable<DateTime> ActivityDays(DateTime month) =>
        Enumerable.Range(1, DateTime.DaysInMonth(month.Year, month.Month))
            .Select(day => month.AddDays(day - 1))
            .Where(day => day >= activityFrom && day <= activityThrough);

    private int ActivityLevel(double seconds) => seconds <= 0 ? 0 : (int)Math.Ceiling(seconds / busiestDay * 4);

    private Task FilterDescriptionAsync(string value)
    {
        text = value;
        return LoadAsync();
    }

    private Task FilterTagAsync(string value)
    {
        tag = value;
        return LoadAsync();
    }

    private Task ResetAsync()
    {
        project = tag = text = "";
        return LoadAsync();
    }

    private bool Overlaps(TimeEntryModel entry) => report!.Entries.Any(e => e.Id != entry.Id && e.Start < (entry.End ?? report.CalculatedAt) && (e.End ?? report.CalculatedAt) > entry.Start);
}
