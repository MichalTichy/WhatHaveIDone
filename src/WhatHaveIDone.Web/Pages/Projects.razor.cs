using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WhatHaveIDone.Contracts;

namespace WhatHaveIDone.Web.Pages;

public partial class Projects : IAsyncDisposable
{
    [Inject] private IJSRuntime Js { get; set; } = null!;
    private string search = "";
    private bool archived, loaded, openPending;
    private ProjectModel? editing;
    private ClientModel? editingClient;
    private bool addingClient;
    private ElementReference dialog;
    private DotNetObjectReference<Projects>? self;
    private IEnumerable<ProjectModel> Visible => User.Projects.Where(p => p.Archived == archived && p.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase));
    protected override Task OnInitializedAsync() => LoadAsync();
    private Task LoadAsync() => RunAsync(async ct => { await User.ReloadAsync(ct); loaded = true; });
    private void Add() { editing = new() { ClientId = User.DefaultClientId }; openPending = true; Error = null; }
    private void Edit(ProjectModel project) { editing = project with { }; openPending = true; Error = null; }
    private void AddClient() { editingClient = new(); addingClient = true; Error = null; }
    private void RenameClient(ClientModel client) { editingClient = client with { }; addingClient = false; Error = null; }
    private Task SaveClientAsync() => RunAsync(async ct =>
    {
        if (editingClient is null) return;
        await Api.SendAsync<string>(addingClient ? HttpMethod.Post : HttpMethod.Put, addingClient ? "clients" : "clients/" + Uri.EscapeDataString(editingClient.Id), editingClient, ct);
        await User.ReloadAsync(ct); editingClient = null;
    });
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!openPending) return; openPending = false;
        self ??= DotNetObjectReference.Create(this); await Js.InvokeVoidAsync("whid.openDialog", Cancellation, dialog, self);
    }
    private Task SaveAsync() => RunAsync(async ct =>
    {
        if (editing is null) return;
        await Api.SendAsync<string>(string.IsNullOrEmpty(editing.Id) ? HttpMethod.Post : HttpMethod.Put, "projects" + (string.IsNullOrEmpty(editing.Id) ? "" : "/" + Uri.EscapeDataString(editing.Id)), editing, ct);
        await CloseAsync(); await User.ReloadAsync(ct);
    });
    private Task ToggleArchiveAsync(ProjectModel project) => RunAsync(async ct => { await Api.SendAsync<string>(HttpMethod.Put, "projects/" + Uri.EscapeDataString(project.Id), project with { Archived = !project.Archived }, ct); await User.ReloadAsync(ct); });
    [JSInvokable] public Task RequestCloseAsync() => CloseAsync();
    private async Task CloseAsync() { if (editing is null) return; await Js.InvokeVoidAsync("whid.closeDialog", Cancellation, dialog); editing = null; }
    public ValueTask DisposeAsync() { Dispose(); self?.Dispose(); return ValueTask.CompletedTask; }
}
