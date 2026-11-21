using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;

namespace WhatHaveIDone.Web.Components.Buttons;

public class AppActionButton : BbButton
{
    private string? additionalClass;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        parameters.TryGetValue<string>(nameof(Class), out additionalClass);
        return base.SetParametersAsync(parameters);
    }

    protected override void OnParametersSet()
    {
        Class = $"app-action {additionalClass}";
        base.OnParametersSet();
    }
}

