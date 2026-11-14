using Microsoft.AspNetCore.Components;
using WhatHaveIDone.API.Models;

namespace WhatHaveIDone.Web.Components.Forms;

public partial class ProjectPicker
{
    [Parameter] public string? Value { get; set; }
    [Parameter] public string? OriginalProject { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
    private readonly string selectId = "project-" + Guid.NewGuid().ToString("N");
    private bool adding;
    private string? clientId;
    private string name = "", color = "#009cde";
    private void Toggle() { adding = !adding; clientId = User.DefaultClientId; Error = null; }
    private Task SelectAsync(ChangeEventArgs args) => ChooseAsync(string.IsNullOrEmpty(args.Value?.ToString()) ? null : args.Value.ToString());
    private Task ChooseAsync(string? id) => Value == id ? Task.CompletedTask : ValueChanged.InvokeAsync(id);
    private Task CreateAsync() => RunAsync(async ct =>
    {
        var id = await Api.SendAsync<string>(HttpMethod.Post, "projects", new ProjectModel { Name = name, Color = color, ClientId = clientId }, ct);
        await User.ReloadAsync(ct);
        if (IsDisposed) return;
        adding = false; name = "";
        await ValueChanged.InvokeAsync(id);
    });
}
