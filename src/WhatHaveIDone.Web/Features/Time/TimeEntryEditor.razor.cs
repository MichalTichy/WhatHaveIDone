using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Web.Resources;
using WhatHaveIDone.Web.Services;

namespace WhatHaveIDone.Web.Features.Time;

public partial class TimeEntryEditor : IAsyncDisposable
{
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Parameter] public TimeEntryModel? Entry { get; set; }
    [Parameter] public DateTimeOffset InitialStart { get; set; }
    [Parameter] public DateTimeOffset InitialEnd { get; set; }
    [Parameter] public bool CreateOnOpen { get; set; }
    [Parameter] public EventCallback<TimeEntryModel> Saved { get; set; }
    [Parameter] public EventCallback Closed { get; set; }
    private ElementReference dialog;
    private DotNetObjectReference<TimeEntryEditor>? self;
    private readonly SemaphoreSlim saveGate = new(1, 1);
    private CancellationTokenSource? debounce;
    private string? id, projectId, originalProject;
    private string description = "", localStart = "", localEnd = "", startOffset = "", endOffset = "", status = "";
    private List<string> selectedTags = [];
    private Guid version, createRequest = Guid.NewGuid();
    private DateTimeOffset originalStart, originalEnd;
    private int revision, savedRevision;
    private bool initialized, running, saving, failed, conflict, closing, overlap, authenticationExpired;
    private (EditTimeEntry Value, int Revision)? pendingCreate;

    protected override void OnParametersSet()
    {
        if (initialized) return;
        initialized = true; Fill(Entry);
    }
    private void Fill(TimeEntryModel? entry)
    {
        id = entry?.Id; version = entry?.Version ?? Guid.Empty; description = entry?.Description ?? "";
        projectId = entry?.ProjectId; originalProject = entry?.ProjectId; selectedTags = [.. entry?.Tags ?? []];
        running = entry is { End: null }; var start = entry?.Start ?? InitialStart; var end = entry?.End ?? InitialEnd;
        originalStart = start; originalEnd = end;
        localStart = UserTime.Input(start, User.Settings.TimeZone); localEnd = UserTime.Input(end, User.Settings.TimeZone);
        startOffset = UserTime.Offset(start, User.Settings.TimeZone); endOffset = UserTime.Offset(end, User.Settings.TimeZone);
        revision = savedRevision = 0; failed = conflict = authenticationExpired = false; Error = null; pendingCreate = null; status = id is null ? "" : Strings.Saved;
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        self = DotNetObjectReference.Create(this);
        await Js.InvokeVoidAsync("whid.openDialog", Cancellation, dialog, self);
        if (CreateOnOpen && Entry is null) await CreateAsync();
        else await CheckOverlapAsync();
    }
    private Task DescriptionChangedAsync(ChangeEventArgs args) { description = args.Value?.ToString() ?? ""; return ChangedAsync(); }
    private async Task TagsChangedAsync(List<string> tags) { selectedTags = tags; revision++; await SaveAsync(); }
    private Task StartChangedAsync(ChangeEventArgs args) { localStart = args.Value?.ToString() ?? ""; return ChangedAsync(); }
    private Task EndChangedAsync(ChangeEventArgs args) { localEnd = args.Value?.ToString() ?? ""; return ChangedAsync(); }
    private async Task ProjectChangedAsync(string? value) { projectId = value; revision++; await SaveAsync(); }
    private async Task ChangedAsync()
    {
        revision++; status = Strings.Unsaved;
        debounce?.Cancel(); debounce?.Dispose(); debounce = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var pending = debounce;
        try { await Task.Delay(500, pending.Token); await SaveAsync(); }
        catch (OperationCanceledException) when (pending.IsCancellationRequested || IsDisposed) { }
    }
    private async Task OffsetChangedAsync() { revision++; await SaveAsync(); }
    private Task FlushAsync() => SaveAsync();
    private async Task CreateAsync() { if (id is null && revision == savedRevision) revision++; await SaveAsync(); }
    private EditTimeEntry Snapshot() => new()
    {
        CreateRequestId = createRequest, Version = version, Description = description, ProjectId = projectId,
        Tags = [.. selectedTags],
        Start = ReadInstant(localStart, startOffset, originalStart), End = running ? null : ReadInstant(localEnd, endOffset, originalEnd)
    };
    private DateTimeOffset ReadInstant(string value, string offset, DateTimeOffset original)
        => value == UserTime.Input(original, User.Settings.TimeZone) && offset == UserTime.Offset(original, User.Settings.TimeZone)
            ? original : UserTime.Parse(value, User.Settings.TimeZone, offset);
    private async Task SaveAsync()
    {
        if (IsDisposed || conflict) return;
        try { await saveGate.WaitAsync(Cancellation); }
        catch (OperationCanceledException) when (Cancellation.IsCancellationRequested) { return; }
        try
        {
            while (!IsDisposed && savedRevision < revision && !conflict)
            {
                var requestRevision = revision;
                EditTimeEntry snapshot;
                try
                {
                    snapshot = Snapshot();
                    if (snapshot.End.HasValue && snapshot.End < snapshot.Start) throw new FormatException(Strings.InvalidInterval);
                }
                catch (FormatException exception) { Error = exception.Message; failed = true; status = Strings.Unsaved; break; }
                if (id is null)
                { pendingCreate ??= (snapshot, requestRevision); snapshot = pendingCreate.Value.Value; requestRevision = pendingCreate.Value.Revision; }
                saving = true; status = Strings.Saving; Error = null; StateHasChanged();
                try
                {
                    var saved = await Api.SendAsync<TimeEntryModel>(id is null ? HttpMethod.Post : HttpMethod.Put, id is null ? "time-entries" : "time-entries/" + Uri.EscapeDataString(id), snapshot with { Version = version }, Cancellation);
                    if (IsDisposed) return;
                    id = saved.Id; version = saved.Version; originalProject = saved.ProjectId; savedRevision = requestRevision; pendingCreate = null;
                    failed = authenticationExpired = false; status = revision == savedRevision ? Strings.Saved : Strings.Unsaved;
                    await Saved.InvokeAsync(saved);
                }
                catch (ApiCallException exception) { if (IsDisposed) return; Error = exception.Message; failed = true; conflict = exception.Status == 409; authenticationExpired = exception.Status == 401; status = Strings.SaveFailed; break; }
                catch (HttpRequestException) { if (IsDisposed) return; Error = Strings.Offline; failed = true; status = Strings.SaveFailed; break; }
                finally { saving = false; }
            }
            if (!failed && !conflict) await CheckOverlapAsync();
        }
        catch (OperationCanceledException) when (Cancellation.IsCancellationRequested) { }
        finally { saveGate.Release(); if (!IsDisposed) StateHasChanged(); }
    }
    private async Task CheckOverlapAsync()
    {
        try
        {
            var snapshot = Snapshot(); var to = snapshot.End ?? DateTimeOffset.UtcNow;
            if (to <= snapshot.Start) return;
            var atRevision = revision;
            var entries = await Api.GetAsync<List<TimeEntryModel>>($"time-entries?from={Uri.EscapeDataString(snapshot.Start.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Cancellation);
            if (!IsDisposed && atRevision == revision) { overlap = entries.Any(e => e.Id != id); StateHasChanged(); }
        }
        catch (FormatException) { }
        catch (ApiCallException) { }
        catch (HttpRequestException) { }
        catch (OperationCanceledException) when (Cancellation.IsCancellationRequested) { }
    }
    [JSInvokable]
    public async Task RequestCloseAsync()
    {
        if (closing) return;
        closing = true;
        try
        {
            debounce?.Cancel(); await SaveAsync();
            if (savedRevision < revision && !await Js.InvokeAsync<bool>("confirm", Cancellation, Strings.DiscardConfirm)) return;
            await Js.InvokeVoidAsync("whid.closeDialog", Cancellation, dialog); await Closed.InvokeAsync();
        }
        finally { if (!IsDisposed) closing = false; }
    }
    private async Task BeforeNavigationAsync(LocationChangingContext context)
    {
        debounce?.Cancel(); await SaveAsync();
        if (savedRevision < revision && !await Js.InvokeAsync<bool>("confirm", Cancellation, Strings.DiscardConfirm)) context.PreventNavigation();
    }
    private async Task ReloadAsync()
    {
        if (id is null || !await Js.InvokeAsync<bool>("confirm", Cancellation, Strings.DiscardConfirm)) return;
        await RunAsync(async ct => { var latest = await Api.GetAsync<TimeEntryModel>("time-entries/" + Uri.EscapeDataString(id), ct); Fill(latest); await CheckOverlapAsync(); });
    }
    private void SignInAgain() => Navigation.NavigateTo("/login", forceLoad: true);
    private async Task DeleteAsync()
    {
        debounce?.Cancel();
        if (id is null || !await Js.InvokeAsync<bool>("confirm", Cancellation, Strings.DeleteConfirm)) return;
        await RunAsync(async ct => { await Api.SendAsync<string>(HttpMethod.Delete, $"time-entries/{Uri.EscapeDataString(id)}?version={version}", null, ct); await Js.InvokeVoidAsync("whid.closeDialog", ct, dialog); await Closed.InvokeAsync(); });
    }
    private bool Ambiguous(string value) => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local) && TimeZoneInfo.FindSystemTimeZoneById(User.Settings.TimeZone).IsAmbiguousTime(DateTime.SpecifyKind(local, DateTimeKind.Unspecified));
    private IEnumerable<string> Offsets(string value) => TimeZoneInfo.FindSystemTimeZoneById(User.Settings.TimeZone).GetAmbiguousTimeOffsets(DateTime.Parse(value, CultureInfo.InvariantCulture)).Select(t => t.ToString());
    public async ValueTask DisposeAsync()
    {
        Dispose(); debounce?.Cancel(); debounce?.Dispose(); self?.Dispose();
        await Task.CompletedTask;
    }
}
