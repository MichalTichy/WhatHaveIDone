using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using WhatHaveIDone.Web.Resources;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Web.Components.Forms;

namespace WhatHaveIDone.Web.Features.Time;

public partial class ProjectPicker
{
    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public string? OriginalProject { get; set; }

    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    private bool adding;
    private string? clientId;
    private string name = "", color = "#009cde";
    private void Toggle()
    {
        adding = !adding;
        clientId = User.DefaultClientId;
        color = ProjectColors.Suggest(User, null);
        Error = null;
    }

    private IReadOnlyList<SelectOption<string>> Options =>
    [
        new("", Strings.NoProject),
        .. User.ProjectsByUsage.Where(p => !p.Archived || p.Id == OriginalProject || p.Id == Value)
            .Select(p => new SelectOption<string>(p.Id, p.Name + " · " + User.ClientName(p.ClientId)))
    ];

    private Task SelectAsync(string? value) => ChooseAsync(string.IsNullOrEmpty(value) ? null : value);
    private Task ChooseAsync(string? id) => Value == id ? Task.CompletedTask : ValueChanged.InvokeAsync(id);
    private Task CreateAsync() => RunAsync(async ct =>
    {
        var id = await Api.SaveProjectAsync(new ProjectModel { Name = name, Color = color, ClientId = clientId }, ct);
        await User.ReloadAsync(ct);
        if (IsDisposed)
            return;
        adding = false;
        name = "";
        await ValueChanged.InvokeAsync(id);
    });
}
