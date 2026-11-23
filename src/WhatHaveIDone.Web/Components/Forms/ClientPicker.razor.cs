using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using WhatHaveIDone.API.Models;

namespace WhatHaveIDone.Web.Components.Forms;

public partial class ClientPicker
{
    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    private IReadOnlyList<SelectOption<string>> Options =>
        User.Clients.Select(client => new SelectOption<string>(client.Id, client.Name)).ToArray();

    private string name = "";
    private bool adding;
    private void Toggle()
    {
        adding = !adding;
        Error = null;
    }

    private Task CreateAsync() => RunAsync(async ct =>
    {
        var id = await Api.SaveClientAsync(new ClientModel { Name = name }, ct);
        await User.ReloadAsync(ct);
        if (IsDisposed)
            return;
        adding = false;
        name = "";
        await ValueChanged.InvokeAsync(id);
    });
}
