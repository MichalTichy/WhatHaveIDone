using Microsoft.AspNetCore.Components;
using WhatHaveIDone.Web.Services;

namespace WhatHaveIDone.Web;

public partial class App
{
    [Inject] private UserContext User { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    private bool ready, failure;
    protected override Task OnInitializedAsync() => InitializeAsync();
    private async Task InitializeAsync()
    {
        ready = false; failure = false;
        try { await User.InitializeAsync(CancellationToken.None); }
        catch (HttpRequestException) { failure = true; }
        catch (ApiCallException) { failure = true; }
        finally { ready = true; }
    }
}
