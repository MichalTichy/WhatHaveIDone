using Microsoft.AspNetCore.Components;

namespace WhatHaveIDone.Web.Components.Forms;

public partial class ProjectColorPicker
{
    [Parameter] public string Value { get; set; } = "#009cde";
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    private static readonly string[] Palette = ["#009cde", "#2563eb", "#4f46e5", "#7c3aed", "#a21caf", "#db2777", "#dc2626", "#ea580c", "#b45309", "#ca8a04", "#65a30d", "#16a34a", "#059669", "#0d9488", "#0891b2", "#687483"];
    private Task ChangeAsync(ChangeEventArgs args) => ValueChanged.InvokeAsync(args.Value?.ToString()?.Trim() ?? "");
}
