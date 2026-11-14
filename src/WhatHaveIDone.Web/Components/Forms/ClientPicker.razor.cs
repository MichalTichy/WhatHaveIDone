using Microsoft.AspNetCore.Components;
using WhatHaveIDone.API.Models;

namespace WhatHaveIDone.Web.Components.Forms;

public partial class ClientPicker
{
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
    private readonly string selectId = "client-" + Guid.NewGuid().ToString("N");
    private string name = "";
    private bool adding;
    private void Toggle() { adding = !adding; Error = null; }
    private Task SelectAsync(ChangeEventArgs args) => ValueChanged.InvokeAsync(args.Value?.ToString());
    private Task CreateAsync() => RunAsync(async ct =>
    {
        var id = await Api.SendAsync<string>(HttpMethod.Post, "clients", new ClientModel { Name = name }, ct);
        await User.ReloadAsync(ct);
        if (IsDisposed) return;
        adding = false; name = "";
        await ValueChanged.InvokeAsync(id);
    });
}
