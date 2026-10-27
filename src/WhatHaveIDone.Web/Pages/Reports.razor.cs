using WhatHaveIDone.API.Models;
using WhatHaveIDone.Web.Resources;
using WhatHaveIDone.Web.Services;

namespace WhatHaveIDone.Web.Pages;

public partial class Reports
{
    private DateTime startDate, endDate;
    private DateTimeOffset appliedFrom, appliedTo;
    private string project = "", tag = "", text = "", appliedQuery = "";
    private bool detail;
    private ReportModel? report;
    private CancellationTokenSource? loadCancellation;
    private int loadGeneration;
    private string CsvUrl => "api/v1/reports.csv?" + appliedQuery;
    protected override Task OnInitializedAsync()
    { var today = UserTime.Local(DateTimeOffset.UtcNow, User.Settings.TimeZone).Date; startDate = new DateTime(today.Year,today.Month,1); endDate = startDate.AddMonths(1).AddDays(-1); return LoadAsync(); }
    private async Task LoadAsync()
    {
        loadCancellation?.Cancel(); loadCancellation?.Dispose();
        loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var ct = loadCancellation.Token; var generation = ++loadGeneration;
        Busy = true; Error = null;
        try
        {
            var from = UserTime.Instant(startDate.Date, User.Settings.TimeZone); var to = UserTime.Instant(endDate.Date.AddDays(1), User.Settings.TimeZone);
            var query = $"from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}&project={Uri.EscapeDataString(project)}&tag={Uri.EscapeDataString(tag)}&text={Uri.EscapeDataString(text)}";
            var selected = await Api.GetAsync<ReportModel>("reports?" + query, ct);
            if (IsDisposed || generation != loadGeneration) return; report = selected; appliedQuery = query; appliedFrom = from; appliedTo = to;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (ApiCallException exception) { if (!IsDisposed && generation == loadGeneration) { Error = exception.Message; if (exception.Status == 401) Navigation.NavigateTo("/login", forceLoad: true); } }
        catch (HttpRequestException) { if (!IsDisposed && generation == loadGeneration) Error = Strings.Offline; }
        finally { if (!IsDisposed && generation == loadGeneration) { Busy = false; StateHasChanged(); } }
    }
    private Task FilterProjectAsync(string? value) { project = value ?? "none"; return LoadAsync(); }
    private Task FilterDayAsync(string value) { startDate = endDate = DateTime.Parse(value, System.Globalization.CultureInfo.InvariantCulture); return LoadAsync(); }
    private Task FilterDescriptionAsync(string value) { text = value; return LoadAsync(); }
    private Task FilterTagAsync(string value) { tag = value; return LoadAsync(); }
    private Task ResetAsync() { project = tag = text = ""; return LoadAsync(); }
    private bool Overlaps(TimeEntryModel entry) => report!.Entries.Any(e => e.Id != entry.Id && e.Start < (entry.End ?? report.CalculatedAt) && (e.End ?? report.CalculatedAt) > entry.Start);
    public override void Dispose() { loadCancellation?.Cancel(); loadCancellation?.Dispose(); base.Dispose(); }
}
