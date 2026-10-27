using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Web.Resources;

namespace WhatHaveIDone.Web.Features.Settings;

public partial class Settings
{
    [Inject] private IJSRuntime Js { get; set; } = null!;
    private SettingsModel settings = new();
    private List<ApiTokenModel> tokens = [];
    private string currentPassword = "", newPassword = "", tokenName = "Riganti IS";
    private string? newToken;
    private bool loaded, importActive;
    private bool mcpSetupOpen;
    private string ApiUrl => Navigation.BaseUri;
    private string McpUrl => new Uri(new Uri(Navigation.BaseUri), "mcp").AbsoluteUri;
    protected override Task OnInitializedAsync() => LoadAsync();
    private Task LoadAsync() => RunAsync(async ct =>
    {
        await User.ReloadAsync(ct); settings = User.Settings with { };
        tokens = await Api.GetAsync<List<ApiTokenModel>>("tokens", ct);
        var imports = await Api.GetAsync<List<ImportModel>>("imports", ct);
        importActive = imports.Any(i => i.Status is "Queued" or "Running" or "Waiting" or "NeedsAttention"); loaded = true;
    });
    private Task SaveSettingsAsync() => RunAsync(async ct => { await Api.SendAsync<bool>(HttpMethod.Put,"settings",settings,ct); await User.ReloadAsync(ct); settings = User.Settings with { }; Feedback.Show(Strings.Saved); });
    private Task ChangePasswordAsync() => RunAsync(async ct => { await Api.SendAsync<bool>(HttpMethod.Post,"account/password",new ChangePasswordInput(currentPassword,newPassword),ct); currentPassword = newPassword = ""; Feedback.Show(Strings.Saved); });
    private Task CreateTokenAsync() => RunAsync(async ct => { newToken = (await Api.SendAsync<CreatedApiToken>(HttpMethod.Post,"tokens",new TokenName(tokenName),ct)).Token; tokens = await Api.GetAsync<List<ApiTokenModel>>("tokens",ct); });
    private Task CopyAsync() => RunAsync(async ct => { await Js.InvokeVoidAsync("whid.copy",ct,newToken); Feedback.Show(Strings.Copied); });
    private async Task RevokeAsync(ApiTokenModel token)
    {
        if(!await Js.InvokeAsync<bool>("confirm",Cancellation,Strings.RevokeConfirm))return;
        await RunAsync(async ct => { await Api.SendAsync<bool>(HttpMethod.Delete,"tokens/"+Uri.EscapeDataString(token.Id),null,ct); tokens = await Api.GetAsync<List<ApiTokenModel>>("tokens",ct); });
    }
}
