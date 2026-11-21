using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Web.Resources;

namespace WhatHaveIDone.Web.Features.Settings;

public partial class Settings
{
    [Inject]
    private IJSRuntime Js { get; set; } = null!;

    private SettingsModel settings = new();
    private List<ApiTokenModel> tokens = [];
    private string currentPassword = "", newPassword = "", tokenName = "Riganti IS";
    private string? newToken;
    private bool loaded, importActive;
    private string ApiUrl => Navigation.BaseUri;

    protected override Task OnInitializedAsync() => LoadAsync();
    private Task LoadAsync() => RunAsync(async ct =>
    {
        await User.ReloadAsync(ct);
        settings = User.Settings with
        {
        };
        tokens = await Api.GetTokensAsync(ct);
        var imports = await Api.GetImportsAsync(ct);
        importActive = imports.Any(i => i.Status.IsActive() && i.Status != ImportStatus.AwaitingConfirmation);
        loaded = true;
    });
    private Task SaveSettingsAsync() => RunAsync(async ct =>
    {
        await Api.SaveSettingsAsync(settings, ct);
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
    private Task CreateTokenAsync() => RunAsync(async ct =>
    {
        newToken = (await Api.CreateTokenAsync(new CreateApiKeyRequest(tokenName), ct)).Token;
        tokens = await Api.GetTokensAsync(ct);
    });
    private Task CopyAsync() => RunAsync(async ct =>
    {
        await Js.InvokeVoidAsync("whid.copy", ct, newToken);
        Feedback.Show(Strings.Copied);
    });
    private async Task RevokeAsync(ApiTokenModel token)
    {
        if (!await Js.InvokeAsync<bool>("confirm", Cancellation, Strings.RevokeConfirm))
            return;
        await RunAsync(async ct =>
        {
            await Api.RevokeTokenAsync(token.Id, ct);
            tokens = await Api.GetTokensAsync(ct);
        });
    }
}
