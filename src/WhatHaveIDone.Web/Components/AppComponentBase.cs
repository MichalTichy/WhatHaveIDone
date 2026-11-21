using Microsoft.AspNetCore.Components;
using WhatHaveIDone.Web.Resources;
using WhatHaveIDone.Web.Services;
using WhatHaveIDone.Web.Services.Api;

namespace WhatHaveIDone.Web.Components;

public abstract class AppComponentBase : ComponentBase, IDisposable
{
    [Inject] protected ApiClient Api { get; set; } = null!;
    [Inject] protected UserContext User { get; set; } = null!;
    [Inject] protected Feedback Feedback { get; set; } = null!;
    [Inject] protected NavigationManager Navigation { get; set; } = null!;

    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? currentLoad;
    private int activeActions;

    protected CancellationToken Cancellation { get; }
    protected AppComponentBase() => Cancellation = lifetime.Token;
    protected bool IsDisposed { get; private set; }
    protected bool Busy => activeActions > 0;
    protected bool IsLoading => currentLoad is not null;
    protected string? Error { get; set; }

    protected async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (IsDisposed) return;
        activeActions++;
        Error = null;
        try { await action(Cancellation); }
        catch (OperationCanceledException) when (Cancellation.IsCancellationRequested) { }
        catch (Exception exception) when (exception is ApiCallException or HttpRequestException or FormatException)
        {
            if (!IsDisposed) ShowError(exception);
        }
        finally
        {
            activeActions--;
            if (!IsDisposed) StateHasChanged();
        }
    }

    protected async Task ExecuteLoadAsync<T>(Func<CancellationToken, Task<T>> read, Action<T> apply)
    {
        if (IsDisposed) return;
        using var load = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var previous = currentLoad;
        currentLoad = load;
        Error = null;

        try
        {
            previous?.Cancel();
            StateHasChanged();
            var result = await read(load.Token);
            if (!IsDisposed && currentLoad == load && !load.IsCancellationRequested) apply(result);
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested) { }
        catch (Exception exception) when (exception is ApiCallException or HttpRequestException or FormatException)
        {
            if (!IsDisposed && currentLoad == load && !load.IsCancellationRequested) ShowError(exception);
        }
        finally
        {
            if (currentLoad == load)
            {
                currentLoad = null;
                if (!IsDisposed) StateHasChanged();
            }
        }
    }

    private void ShowError(Exception exception)
    {
        Error = exception is HttpRequestException ? Strings.Offline : exception.Message;
        if (exception is ApiCallException { Status: 401 }) Navigation.NavigateTo("/login", forceLoad: true);
    }

    public virtual void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        lifetime.Cancel();
        lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
