using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Web.Resources;

namespace WhatHaveIDone.Web.Features.Settings;

public partial class ApiKeys
{
    [Inject]
    private IJSRuntime Js { get; set; } = null!;

    private List<ApiTokenModel> tokens = [];
    private string tokenName = "Riganti IS";
    private string? newToken;
    private bool loaded, importActive, mcpSetupOpen;
    private string ApiUrl => Navigation.BaseUri;
    private string McpUrl => new Uri(new Uri(Navigation.BaseUri), "mcp").AbsoluteUri;

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() => RunAsync(async ct =>
    {
        tokens = await Api.GetTokensAsync(ct);
        var imports = await Api.GetImportsAsync(ct);
        importActive = imports.Any(i => i.Status.IsActive() && i.Status != ImportStatus.AwaitingConfirmation);
        loaded = true;
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
        {
            return;
        }
        await RunAsync(async ct =>
        {
            await Api.RevokeTokenAsync(token.Id, ct);
            tokens = await Api.GetTokensAsync(ct);
        });
    }
}
