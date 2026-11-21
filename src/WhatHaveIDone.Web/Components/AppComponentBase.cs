using Microsoft.AspNetCore.Components;
using WhatHaveIDone.Web.Resources;
using WhatHaveIDone.Web.Services;

namespace WhatHaveIDone.Web.Components;

public abstract class AppComponentBase : ComponentBase, IDisposable
{
    [Inject] protected ApiClient Api { get; set; } = null!;
    [Inject] protected UserContext User { get; set; } = null!;
    [Inject] protected Feedback Feedback { get; set; } = null!;
    [Inject] protected NavigationManager Navigation { get; set; } = null!;
    private readonly CancellationTokenSource lifetime = new();
    protected CancellationToken Cancellation { get; }
    protected AppComponentBase() => Cancellation = lifetime.Token;
    protected bool IsDisposed { get; private set; }
    protected bool Busy { get; set; }
    protected string? Error { get; set; }
    protected async Task RunAsync(Func<CancellationToken, Task> action)
    {
        Busy = true; Error = null;
        try { await action(Cancellation); }
        catch (OperationCanceledException) when (Cancellation.IsCancellationRequested) { }
        catch (ApiCallException exception) { if (!IsDisposed) { Error = exception.Message; if (exception.Status == 401) Navigation.NavigateTo("/login", forceLoad: true); } }
        catch (HttpRequestException) { if (!IsDisposed) Error = Strings.Offline; }
        catch (FormatException exception) { if (!IsDisposed) Error = exception.Message; }
        finally { if (!IsDisposed) { Busy = false; StateHasChanged(); } }
    }
    public virtual void Dispose() { if (IsDisposed) return; IsDisposed = true; lifetime.Cancel(); lifetime.Dispose(); GC.SuppressFinalize(this); }
}
