using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WhatHaveIDone.Web.Services;
using WhatHaveIDone.Web.Services.Api;
using WhatHaveIDone.Web.Resources;

namespace WhatHaveIDone.Web.Layout;

public partial class MainLayout : IAsyncDisposable
{
    [Inject]
    private UserContext User { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private ApiClient Api { get; set; } = null!;

    [Inject]
    private Feedback Feedback { get; set; } = null!;

    [Inject]
    private IJSRuntime Js { get; set; } = null!;

    private bool menuOpen, updateAvailable;
    private bool feedbackOpen;

    private void OpenFeedback()
    {
        CloseMenu();
        feedbackOpen = true;
    }

    private void CloseFeedback() => feedbackOpen = false;
    private DotNetObjectReference<MainLayout>? self;
    private string? updateWatcher, installWatcher, installMode;
    private bool installHintVisible;
    protected override void OnInitialized() => Feedback.Changed += OnFeedback;
    protected override void OnParametersSet()
    {
        var path = Navigation.ToBaseRelativePath(Navigation.Uri).Split('?', '#')[0].TrimEnd('/');
        if (!User.Session.Authenticated)
        {
            Navigation.NavigateTo("/login", replace: true);
        }
        else if (!User.Settings.OnboardingCompleted && path is not ("import" or "settings/import"))
        {
            Navigation.NavigateTo("/settings/import", replace: true);
        }
    }

    private void OnFeedback() => _ = InvokeAsync(StateHasChanged);
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;
        self = DotNetObjectReference.Create(this);
        updateWatcher = await Js.InvokeAsync<string>("whid.watchUpdates", self);
        updateAvailable = await Js.InvokeAsync<bool>("whid.updateAvailable");
        installWatcher = await Js.InvokeAsync<string>("whid.watchInstall", self);
        installMode = await Js.InvokeAsync<string?>("whid.installMode");
        if (updateAvailable || installMode is not null)
            StateHasChanged();
    }

    [JSInvokable]
    public Task UpdateAvailableAsync()
    {
        updateAvailable = true;
        return InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public Task InstallModeChangedAsync(string? mode)
    {
        installMode = mode;
        return InvokeAsync(StateHasChanged);
    }

    private async Task InstallAsync()
    {
        if (installMode == "ios")
        {
            installHintVisible = !installHintVisible;
            return;
        }

        await Js.InvokeVoidAsync("whid.install");
    }

    private async Task UpdateAsync() => await Js.InvokeVoidAsync("whid.applyUpdate");
    private void ToggleMenu() => menuOpen = !menuOpen;
    private void CloseMenu() => menuOpen = false;
    private async Task SignOutAsync()
    {
        try
        {
            await Api.LogoutAsync(CancellationToken.None);
            Navigation.NavigateTo("/login", forceLoad: true);
        }
        catch (ApiCallException exception) when (exception.Status == 401)
        {
            Navigation.NavigateTo("/login", forceLoad: true);
        }
        catch (ApiCallException exception)
        {
            Feedback.Show(exception.Message);
        }
        catch (HttpRequestException)
        {
            Feedback.Show(Strings.Offline);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Feedback.Changed -= OnFeedback;
        try
        {
            if (updateWatcher is not null)
                await Js.InvokeVoidAsync("whid.unwatchUpdates", updateWatcher);
            if (installWatcher is not null)
                await Js.InvokeVoidAsync("whid.unwatchInstall", installWatcher);
        }
        catch (JSDisconnectedException)
        {
        }

        self?.Dispose();
    }
}
