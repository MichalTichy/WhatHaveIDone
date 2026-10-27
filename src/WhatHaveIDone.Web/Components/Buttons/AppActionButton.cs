using LumexUI;
using LumexUI.Common;
using Microsoft.AspNetCore.Components;

namespace WhatHaveIDone.Web.Components.Buttons;

public class AppActionButton : LumexButton
{
    private string? additionalClass;

    public AppActionButton()
    {
        Type = ButtonType.Button;
        Color = ThemeColor.Primary;
        Radius = Radius.Medium;
    }

    protected virtual string ButtonClass => "min-h-11 max-w-full gap-2 px-5 text-sm whitespace-normal";

    public override Task SetParametersAsync(ParameterView parameters)
    {
        parameters.TryGetValue<string>(nameof(Class), out additionalClass);
        return base.SetParametersAsync(parameters);
    }

    protected override void OnParametersSet()
    {
        Class = $"{ButtonClass} {additionalClass}";
        base.OnParametersSet();
    }
}

