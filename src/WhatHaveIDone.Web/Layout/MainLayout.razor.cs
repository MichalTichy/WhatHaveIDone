using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WhatHaveIDone.Web.Services;
using WhatHaveIDone.Web.Resources;

namespace WhatHaveIDone.Web.Layout;

public partial class MainLayout : IAsyncDisposable
{
    [Inject] private UserContext User { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private ApiClient Api { get; set; } = null!;
    [Inject] private Feedback Feedback { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;
    private bool menuOpen,updateAvailable;
    private DotNetObjectReference<MainLayout>? self;
    private string? updateWatcher;
    protected override void OnInitialized() => Feedback.Changed += OnFeedback;
    protected override void OnParametersSet()
    {
        if (!User.Session.Authenticated) Navigation.NavigateTo("/login", replace: true);
        else if (!User.Settings.OnboardingCompleted && !Navigation.ToBaseRelativePath(Navigation.Uri).StartsWith("import", StringComparison.Ordinal)) Navigation.NavigateTo("/import", replace: true);
    }
    private void OnFeedback() => _ = InvokeAsync(StateHasChanged);
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {if(!firstRender)return;self=DotNetObjectReference.Create(this);updateWatcher=await Js.InvokeAsync<string>("whid.watchUpdates",self);updateAvailable=await Js.InvokeAsync<bool>("whid.updateAvailable");if(updateAvailable)StateHasChanged();}
    [JSInvokable] public Task UpdateAvailableAsync(){updateAvailable=true;return InvokeAsync(StateHasChanged);}
    private async Task UpdateAsync()=>await Js.InvokeVoidAsync("whid.applyUpdate");
    private void ToggleMenu() => menuOpen = !menuOpen;
    private void CloseMenu() => menuOpen = false;
    private async Task SignOutAsync()
    {
        try
        {
            await Api.SendAsync<bool>(HttpMethod.Post, "account/logout", null, CancellationToken.None);
            Navigation.NavigateTo("/login", forceLoad: true);
        }
        catch (ApiCallException exception) when (exception.Status == 401) { Navigation.NavigateTo("/login", forceLoad: true); }
        catch (ApiCallException exception) { Feedback.Show(exception.Message); }
        catch (HttpRequestException) { Feedback.Show(Strings.Offline); }
    }
    public async ValueTask DisposeAsync(){Feedback.Changed-=OnFeedback;try{if(updateWatcher is not null)await Js.InvokeVoidAsync("whid.unwatchUpdates",updateWatcher);}catch(JSDisconnectedException){}self?.Dispose();}
}
