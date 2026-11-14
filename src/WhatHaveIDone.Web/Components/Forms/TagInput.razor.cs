using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using WhatHaveIDone.Web.Resources;
using WhatHaveIDone.Web.Services;

namespace WhatHaveIDone.Web.Components.Forms;

public partial class TagInput
{
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Parameter] public List<string> Value { get; set; } = [];
    [Parameter] public EventCallback<List<string>> ValueChanged { get; set; }
    [Parameter] public string? ProjectId { get; set; }
    private readonly string inputId = "tags-" + Guid.NewGuid().ToString("N");
    private ElementReference input;
    private string text = "";
    private string? loadedProject, suggestionError;
    private IReadOnlyList<string> recentTags = [];
    private CancellationTokenSource? suggestionCancellation;
    private int suggestionGeneration;
    private bool loadingSuggestions;
    private IEnumerable<string> SuggestedTags => recentTags.Where(t => !Value.Contains(t, StringComparer.OrdinalIgnoreCase));

    protected override Task OnParametersSetAsync()
    {
        if (loadedProject == ProjectId) return Task.CompletedTask;
        loadedProject = ProjectId;
        return LoadSuggestionsAsync();
    }
    private async Task LoadSuggestionsAsync()
    {
        suggestionCancellation?.Cancel(); suggestionCancellation?.Dispose();
        suggestionCancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var ct = suggestionCancellation.Token; var generation = ++suggestionGeneration;
        recentTags = []; suggestionError = null; loadingSuggestions = false;
        if (string.IsNullOrEmpty(ProjectId)) return;
        loadingSuggestions = true;
        try
        {
            var tags = await Api.GetAsync<List<string>>("projects/" + Uri.EscapeDataString(ProjectId) + "/recent-tags", ct);
            if (!IsDisposed && generation == suggestionGeneration) recentTags = tags;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (ApiCallException exception)
        {
            if (!IsDisposed && generation == suggestionGeneration)
            {
                suggestionError = exception.Message;
                if (exception.Status == 401) Navigation.NavigateTo("/login", forceLoad: true);
            }
        }
        catch (HttpRequestException) { if (!IsDisposed && generation == suggestionGeneration) suggestionError = Strings.Offline; }
        finally { if (!IsDisposed && generation == suggestionGeneration) { loadingSuggestions = false; StateHasChanged(); } }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await Js.InvokeVoidAsync("whid.tagKeys", Cancellation, input);
    }
    private void InputChanged(ChangeEventArgs args) => text = args.Value?.ToString() ?? "";
    private async Task CommitAsync()
    {
        var names = text.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToArray();
        if (names.Length == 0) { text = ""; return; }
        text = "";
        await AddAsync(names);
    }
    private Task AddAsync(IEnumerable<string> names) => ValueChanged.InvokeAsync(Value.Concat(names).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    private Task RemoveAsync(string tag) => ValueChanged.InvokeAsync(Value.Where(t => t != tag).ToList());
    private Task KeyDownAsync(KeyboardEventArgs args) => args.Key is "Enter" or "," ? CommitAsync() : Task.CompletedTask;
    public override void Dispose()
    {
        if (IsDisposed) return;
        suggestionCancellation?.Cancel(); suggestionCancellation?.Dispose(); base.Dispose();
    }
}
