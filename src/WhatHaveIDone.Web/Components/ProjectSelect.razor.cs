using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;

namespace WhatHaveIDone.Web.Components;

public partial class ProjectSelect
{
    [Parameter, EditorRequired]
    public IReadOnlyList<SelectOption<string>> Options { get; set; } = [];

    [Parameter]
    public string Value { get; set; } = "";

    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    private readonly string selectId = "project-" + Guid.NewGuid().ToString("N");
    private string search = "";
    private IReadOnlyList<SelectOption<string>> Matches => Options
        .Where(option => option.Text.Contains(search.Trim(), StringComparison.CurrentCultureIgnoreCase))
        .ToList();

    private void UpdateSearch(string? value) => search = value ?? "";

    private async Task SelectAsync(string? value)
    {
        search = "";
        await ValueChanged.InvokeAsync(value);
    }
}
