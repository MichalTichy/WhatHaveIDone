using BlazorBlueprint.Primitives;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Web.Resources;
using WhatHaveIDone.Web.Services;

namespace WhatHaveIDone.Web.Features.Reporting;

public partial class Reports
{
    private DateTime startDate, endDate;
    private DateTimeOffset appliedFrom, appliedTo;
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

    private void SelectProject(string? value) => project = value ?? "";

    protected override Task OnInitializedAsync()
    {
        var today = UserTime.Local(DateTimeOffset.UtcNow, User.Settings.TimeZone).Date;
        startDate = new DateTime(today.Year, today.Month, 1);
        endDate = startDate.AddMonths(1).AddDays(-1);
        return LoadAsync();
    }

    private Task LoadAsync()
    {
        var from = UserTime.Instant(startDate.Date, User.Settings.TimeZone);
        var to = UserTime.Instant(endDate.Date.AddDays(1), User.Settings.TimeZone);
        var filters = (Project: project, Tag: tag, Text: text);
        return ExecuteLoadAsync(ct => Api.GetReportAsync(from, to, filters.Project, filters.Tag, filters.Text, ct), selected =>
        {
            report = selected;
            CsvUrl = Api.GetReportCsvUrl(from, to, filters.Project, filters.Tag, filters.Text);
            appliedFrom = from;
            appliedTo = to;
        });
    }

    private Task FilterProjectAsync(string? value)
    {
        project = value ?? "none";
        return LoadAsync();
    }

    private Task FilterDayAsync(string value)
    {
        startDate = endDate = DateTime.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        return LoadAsync();
    }

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
