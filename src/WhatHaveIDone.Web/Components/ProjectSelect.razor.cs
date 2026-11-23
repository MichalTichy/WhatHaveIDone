using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace WhatHaveIDone.Web.Components;

public partial class ProjectSelect
{
    [Inject]
    private IJSRuntime Js { get; set; } = null!;

    [Parameter, EditorRequired]
    public IReadOnlyList<SelectOption<string>> Options { get; set; } = [];

    [Parameter]
    public string Value { get; set; } = "";

    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    [Parameter]
    public RenderFragment? Action { get; set; }

    private readonly string triggerId = "project-" + Guid.NewGuid().ToString("N");
    private readonly string listId = "project-list-" + Guid.NewGuid().ToString("N");
    private ElementReference popover, trigger;
    private string search = "";
    private string? SelectedText => Options.FirstOrDefault(option => option.Value == Value)?.Text;

    private async Task FocusSearchAsync()
    {
        search = "";
        await Js.InvokeVoidAsync("whid.focusPopoverSearch", popover, trigger);
    }

    private async Task SelectAsync(string selection)
    {
        await Js.InvokeVoidAsync("whid.closePopover", popover, trigger);
        await ValueChanged.InvokeAsync(selection["project:".Length..]);
    }
}
