using LumexUI.Common;
using Microsoft.AspNetCore.Components;

namespace WhatHaveIDone.Web.Components.Buttons;

public class AppStatusButton : AppActionButton
{
    [Parameter] public bool Active { get; set; }

    public AppStatusButton() => Variant = Variant.Outlined;

    protected override string ButtonClass => base.ButtonClass + (Active
        ? " border border-danger-200 bg-danger-50 text-danger"
        : " border border-primary-200 bg-primary-50 text-primary-700");

    protected override void OnParametersSet()
    {
        Color = Active ? ThemeColor.Danger : ThemeColor.Primary;
        base.OnParametersSet();
    }
}

