using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Web.Resources;

namespace WhatHaveIDone.Web.Features.Settings;

public partial class Clients
{
    [Inject]
    private IJSRuntime Js { get; set; } = null!;

    private ClientModel? editing;
    private bool adding, loaded;

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() => RunAsync(async ct =>
    {
        await User.ReloadAsync(ct);
        loaded = true;
    });

    private void Add()
    {
        editing = new();
        adding = true;
        Error = null;
    }

    private void Rename(ClientModel client)
    {
        editing = client with
        {
        };
        adding = false;
        Error = null;
    }

    private Task SaveAsync() => RunAsync(async ct =>
    {
        if (editing is null)
            return;
        await Api.SaveClientAsync(editing, ct);
        await User.ReloadAsync(ct);
        editing = null;
    });

    private async Task DeleteAsync(ClientModel client)
    {
        if (!await Js.InvokeAsync<bool>("confirm", Cancellation, string.Format(Strings.DeleteClientConfirm, client.Name)))
            return;
        await RunAsync(async ct =>
        {
            await Api.DeleteClientAsync(client.Id, client.Version, ct);
            if (editing?.Id == client.Id)
                editing = null;
            await User.ReloadAsync(ct);
        });
    }
}
