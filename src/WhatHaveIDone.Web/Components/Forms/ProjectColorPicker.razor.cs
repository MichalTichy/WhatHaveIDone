using Microsoft.AspNetCore.Components;

namespace WhatHaveIDone.Web.Components.Forms;

public partial class ProjectColorPicker
{
    [Parameter] public string Value { get; set; } = "#009cde";
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    // The edited project is excluded so its own colour does not push the suggestion away.
    [Parameter] public string? ProjectId { get; set; }

    private string Suggested => ProjectColors.Suggest(User, ProjectId);
    private Task ChangeAsync(ChangeEventArgs args) => ValueChanged.InvokeAsync(args.Value?.ToString()?.Trim() ?? "");
}
