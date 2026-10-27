using LumexUI;
using LumexUI.Common;

namespace WhatHaveIDone.Web.Components.Forms;

public class AppTextbox : LumexTextbox
{
    private readonly InputFieldSlots defaultClasses = new();

    public AppTextbox()
    {
        Variant = InputVariant.Outlined;
        LabelPlacement = LabelPlacement.Outside;
        Classes = defaultClasses;
    }

    protected override void OnParametersSet()
    {
        // Lumex otherwise moves an outside label into an empty, unfocused input.
        defaultClasses.Label = LabelPlacement is LabelPlacement.Outside
            ? "text-xs font-bold left-0! " + (Size switch
            {
                Size.Small => "-translate-y-[calc(100%_+_var(--text-tiny)/2_+_16px)]!",
                Size.Large => "-translate-y-[calc(100%_+_var(--text-small)/2_+_24px)]!",
                _ => "-translate-y-[calc(100%_+_var(--text-small)/2_+_20px)]!"
            })
            : string.Empty;

        base.OnParametersSet();
    }

    protected override ValueTask SetValidationMessageAsync()
    {
        // The form owns Invalid and localized ErrorMessage; native validity follows the browser language.
        return ValueTask.CompletedTask;
    }
}

