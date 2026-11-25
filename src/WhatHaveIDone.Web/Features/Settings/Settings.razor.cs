using BlazorBlueprint.Primitives;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Web.Resources;

namespace WhatHaveIDone.Web.Features.Settings;

public partial class Settings
{
    private SettingsModel settings = new();
    private string currentPassword = "", newPassword = "";
    private bool loaded;

    private static readonly IReadOnlyList<SelectOption<string>> SystemTimeZoneOptions = TimeZoneInfo.GetSystemTimeZones()
        .Select(zone => new SelectOption<string>(zone.Id, zone.Id)).ToArray();

    private IReadOnlyList<SelectOption<string>> TimeZoneOptions =>
        SystemTimeZoneOptions.Any(option => option.Value == settings.TimeZone)
            ? SystemTimeZoneOptions
            : [new(settings.TimeZone, settings.TimeZone), .. SystemTimeZoneOptions];

    private void SelectTimeZone(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            settings.TimeZone = value;
        }
    }

    protected override Task OnInitializedAsync() => LoadAsync();
    private Task LoadAsync() => RunAsync(async ct =>
    {
        await User.ReloadAsync(ct);
        settings = User.Settings with
        {
        };
        loaded = true;
    });

    private Task SaveSettingsAsync() => RunAsync(async ct =>
    {
        var languageChanged = settings.Language != User.Settings.Language;
        await Api.SaveSettingsAsync(settings, ct);
        if (languageChanged)
        {
            Navigation.NavigateTo(Navigation.Uri, forceLoad: true);
            return;
        }
        await User.ReloadAsync(ct);
        settings = User.Settings with
        {
        };
        Feedback.Show(Strings.Saved);
    });

    private Task ChangePasswordAsync() => RunAsync(async ct =>
    {
        await Api.ChangePasswordAsync(new ChangePasswordInput(currentPassword, newPassword), ct);
        currentPassword = newPassword = "";
        Feedback.Show(Strings.Saved);
    });
}
