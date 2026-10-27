using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WhatHaveIDone.Contracts;
using WhatHaveIDone.Web.Resources;
using WhatHaveIDone.Web.Services;

namespace WhatHaveIDone.Web.Pages;

public partial class Time : IAsyncDisposable
{
    [Inject] private IJSRuntime Js { get; set; } = null!;
    private List<TimeEntryModel> entries = [];
    private TimeEntryModel? runningEntry, editingEntry;
    private string description = "";
    private string? projectId;
    private List<string> selectedTags = [];
    private DateTime selectedDay;
    private DateTimeOffset now = DateTimeOffset.UtcNow, from, to, editorStart, editorEnd;
    private bool calendarView, narrow, loading, loaded, editorOpen, calendarDirty, createEditorEntry;
    private int loadGeneration;
    private Guid editorKey;
    private CancellationTokenSource? loadCancellation;
    private readonly PeriodicTimer timer = new(TimeSpan.FromSeconds(1));
    private Task? timerTask;
    private ElementReference calendarElement;
    private IJSObjectReference? calendarModule;
    private DotNetObjectReference<Time>? self;
    private string? watcher;
    private bool calendarCreated;
    protected override async Task OnInitializedAsync()
    {
        selectedDay = UserTime.Local(now, User.Settings.TimeZone).Date;
        timerTask = TickAsync(); await LoadAsync();
    }
    private void SetRange()
    {
        var start = narrow ? selectedDay.Date : selectedDay.Date.AddDays(-((7 + (int)selectedDay.DayOfWeek - User.Settings.FirstDayOfWeek) % 7));
        from = UserTime.Instant(start, User.Settings.TimeZone); to = UserTime.Instant(start.AddDays(narrow ? 1 : 7), User.Settings.TimeZone);
    }
    private async Task LoadAsync()
    {
        loadCancellation?.Cancel(); loadCancellation?.Dispose(); loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var ct = loadCancellation.Token; var generation = ++loadGeneration; loading = true; SetRange();
        try
        {
            var selected = await Api.GetAsync<List<TimeEntryModel>>($"time-entries?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", ct);
            var running = await Api.GetAsync<TimeEntryModel?>("timer", ct);
            if (IsDisposed || generation != loadGeneration) return;
            entries = selected; runningEntry = running; loaded = true; Error = null; calendarDirty = true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (ApiCallException exception) { if (!IsDisposed && generation == loadGeneration) { Error = exception.Message; if (exception.Status == 401) Navigation.NavigateTo("/login", forceLoad: true); } }
        catch (HttpRequestException) { if (!IsDisposed && generation == loadGeneration) Error = Strings.Offline; }
        finally { if (!IsDisposed && generation == loadGeneration) { loading = false; StateHasChanged(); } }
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            self = DotNetObjectReference.Create(this);
            narrow = await Js.InvokeAsync<bool>("whid.isNarrow", Cancellation);
            watcher = await Js.InvokeAsync<string>("whid.watchPage", Cancellation, self); await LoadAsync();
        }
        if (calendarView && loaded && calendarDirty)
        {
            calendarDirty = false; calendarModule ??= await Js.InvokeAsync<IJSObjectReference>("import", Cancellation, "./calendar.js");
            var options = new { date = selectedDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), timeZone = User.Settings.TimeZone, locale = User.Settings.Language, firstDay = User.Settings.FirstDayOfWeek,
                dayTotals = Days().ToDictionary(day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), day => UserTime.Duration(DaySeconds(day, DayEntries(day)))),
                events = entries.Select(e => new { id = e.Id, title = (e.End == e.Start ? UserTime.Duration(0) + " · " : "") + (string.IsNullOrWhiteSpace(e.Description) ? Strings.NoDescription : e.Description), start = e.Start.ToString("O"),
                    // FullCalendar treats equal endpoints as a default one-hour event. A display-only point keeps zero records visible; their exact values are edited in the form.
                    end = (e.End == e.Start ? e.Start.AddMilliseconds(1) : e.End ?? now).ToString("O"), zeroDuration = e.End == e.Start, color = User.ProjectColor(e.ProjectId),
                    backgroundColor = $"color-mix(in srgb, {User.ProjectColor(e.ProjectId)} 13%, white)", textColor = "#393440", editable = e.End.HasValue && e.End > e.Start }) };
            if (!calendarCreated) { await calendarModule.InvokeVoidAsync("create", Cancellation, calendarElement, self, options); calendarCreated = true; }
            else await calendarModule.InvokeVoidAsync("update", Cancellation, calendarElement, options);
        }
    }
    private async Task TickAsync()
    {
        try { while (await timer.WaitForNextTickAsync(Cancellation)) { now = DateTimeOffset.UtcNow; if (now.Second == 0) calendarDirty = true; await InvokeAsync(StateHasChanged); } }
        catch (OperationCanceledException) when (Cancellation.IsCancellationRequested) { }
    }
    private Task PreviousAsync() { selectedDay = selectedDay.AddDays(narrow ? -1 : -7); return LoadAsync(); }
    private Task NextAsync() { selectedDay = selectedDay.AddDays(narrow ? 1 : 7); return LoadAsync(); }
    private Task TodayAsync() { selectedDay = UserTime.Local(now, User.Settings.TimeZone).Date; return LoadAsync(); }
    private async Task ShowList() { await DisposeCalendarAsync(); calendarView = false; }
    private void ShowCalendar() { calendarView = true; calendarDirty = true; }
    private Task StartAsync() => RunAsync(async ct => { await Api.SendAsync<TimeEntryModel>(HttpMethod.Post, "timer/start", new EditTimeEntry { Description = description, ProjectId = projectId, Tags = selectedTags }, ct); await User.ReloadAsync(ct); await LoadAsync(); });
    private Task StopAsync() => RunAsync(async ct => { if (runningEntry is not null) await Api.SendAsync<bool>(HttpMethod.Post, "timer/" + Uri.EscapeDataString(runningEntry.Id) + "/stop", null, ct); await LoadAsync(); });
    private Task ContinueAsync(TimeEntryModel entry) => RunAsync(async ct => { var project = User.Projects.FirstOrDefault(p => p.Id == entry.ProjectId && !p.Archived); await Api.SendAsync<TimeEntryModel>(HttpMethod.Post, "timer/start", new EditTimeEntry { Description = entry.Description, ProjectId = project?.Id, Tags = entry.Tags }, ct); await LoadAsync(); });
    private Task AddAsync() { editingEntry = null; editorStart = UserTime.Instant(selectedDay.Date.AddHours(9), User.Settings.TimeZone); editorEnd = editorStart.AddHours(1); ShowEditor(); return Task.CompletedTask; }
    private void ShowEditor(bool createEntry = false) { createEditorEntry = createEntry; editorKey = Guid.NewGuid(); editorOpen = true; }
    private void FillGap(DateTimeOffset start, DateTimeOffset end)
    {
        if (editorOpen || loading) return;
        editingEntry = null; editorStart = start; editorEnd = end;
        ShowEditor(createEntry: true);
    }
    [JSInvokable] public Task OpenAsync(string id) { editingEntry = entries.FirstOrDefault(e => e.Id == id); if (editingEntry is not null) ShowEditor(); StateHasChanged(); return Task.CompletedTask; }
    [JSInvokable] public Task SelectAsync(string start, string end, bool fillGap = false) { if (editorOpen) return Task.CompletedTask; editingEntry = null; editorStart = DateTimeOffset.Parse(start, CultureInfo.InvariantCulture); editorEnd = DateTimeOffset.Parse(end, CultureInfo.InvariantCulture); ShowEditor(createEntry: fillGap); StateHasChanged(); return Task.CompletedTask; }
    [JSInvokable] public async Task<bool> MoveAsync(string id, string start, string end)
    {
        var entry = entries.FirstOrDefault(e => e.Id == id); if (entry is null || entry.End is null) return false;
        var success = false;
        await RunAsync(async ct => { await Api.SendAsync<TimeEntryModel>(HttpMethod.Put, "time-entries/" + Uri.EscapeDataString(id), new EditTimeEntry { Version = entry.Version, Description = entry.Description, ProjectId = entry.ProjectId, Tags = entry.Tags, Start = DateTimeOffset.Parse(start, CultureInfo.InvariantCulture), End = DateTimeOffset.Parse(end, CultureInfo.InvariantCulture) }, ct); success = true; await LoadAsync(); });
        return success;
    }
    [JSInvokable] public Task RefreshAsync() => RunAsync(async ct => { await User.ReloadAsync(ct); await LoadAsync(); });
    [JSInvokable] public async Task ViewportAsync(bool value) { if (narrow == value) return; narrow = value; await LoadAsync(); }
    private Task OnSavedAsync(TimeEntryModel entry) => RunAsync(async ct => { await User.ReloadAsync(ct); await LoadAsync(); });
    private async Task OnClosedAsync() { editorOpen = false; await LoadAsync(); }
    private IEnumerable<DateTime> Days() { var start = UserTime.Local(from, User.Settings.TimeZone).Date; for (var day = start; day < UserTime.Local(to, User.Settings.TimeZone).Date; day = day.AddDays(1)) yield return day; }
    private IEnumerable<TimeEntryModel> DayEntries(DateTime day) { var start = UserTime.Instant(day, User.Settings.TimeZone); var end = UserTime.Instant(day.AddDays(1), User.Settings.TimeZone); return entries.Where(e => e.Start < end && ((e.End ?? now) > start || e.End == e.Start && e.Start >= start)); }
    private IEnumerable<(TimeEntryModel? Entry, DateTimeOffset Start, DateTimeOffset End)> DayRows(DateTime day, List<TimeEntryModel> daily)
    {
        var dayStart = UserTime.Instant(day, User.Settings.TimeZone);
        var dayEnd = UserTime.Instant(day.AddDays(1), User.Settings.TimeZone);
        List<(TimeEntryModel? Entry, DateTimeOffset Start, DateTimeOffset End)> rows = [];
        DateTimeOffset? coveredUntil = null;
        foreach (var entry in daily.OrderBy(e => e.Start).ThenByDescending(e => e.End ?? now))
        {
            var start = entry.Start > dayStart ? entry.Start : dayStart;
            var end = (entry.End ?? now) < dayEnd ? entry.End ?? now : dayEnd;
            if (coveredUntil.HasValue && start > coveredUntil.Value) rows.Add((null, coveredUntil.Value, start));
            rows.Add((entry, start, end));
            if (!coveredUntil.HasValue || end > coveredUntil.Value) coveredUntil = end;
        }
        return rows.AsEnumerable().Reverse();
    }
    private string GapTime(DateTimeOffset instant)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(User.Settings.TimeZone);
        var local = TimeZoneInfo.ConvertTime(instant, zone);
        return local.ToString(zone.IsAmbiguousTime(local.DateTime) ? "HH:mm zzz" : "HH:mm");
    }
    private double DaySeconds(DateTime day, IEnumerable<TimeEntryModel> daily) => daily.Sum(e => UserTime.Seconds(e.Start, e.End, now, UserTime.Instant(day, User.Settings.TimeZone), UserTime.Instant(day.AddDays(1), User.Settings.TimeZone)));
    private double TotalSeconds() => entries.Sum(e => UserTime.Seconds(e.Start, e.End, now, from, to));
    private bool Overlaps(TimeEntryModel entry) => entries.Any(e => e.Id != entry.Id && e.Start < (entry.End ?? now) && (e.End ?? now) > entry.Start);
    private async Task DisposeCalendarAsync() { if (calendarCreated && calendarModule is not null) { await calendarModule.InvokeVoidAsync("dispose", calendarElement); calendarCreated = false; } }
    public async ValueTask DisposeAsync()
    {
        Dispose(); timer.Dispose(); loadCancellation?.Cancel(); loadCancellation?.Dispose();
        if (timerTask is not null) await timerTask;
        try { if (watcher is not null) await Js.InvokeVoidAsync("whid.unwatchPage", watcher); await DisposeCalendarAsync(); if (calendarModule is not null) await calendarModule.DisposeAsync(); } catch (JSDisconnectedException) { }
        self?.Dispose();
    }
}
