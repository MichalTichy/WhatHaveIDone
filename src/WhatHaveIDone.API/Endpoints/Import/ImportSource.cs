using WhatHaveIDone.API.Import;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Endpoints;

internal static class ImportSource
{
    public static async Task<TogglImportSource> ReadAsync(TogglImportClient toggl, string token, TimeProvider clock, HttpResponse response,
        CancellationToken cancellationToken)
    {
        try { return await toggl.GetSourceAsync(token, cancellationToken); }
        catch (TogglImportException exception)
        {
            if (exception.NextAttempt is { } next)
            {
                response.Headers.RetryAfter = Math.Max(1, Math.Ceiling((next - clock.GetUtcNow()).TotalSeconds))
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
                throw new AppException("TooManyRequests", 429);
            }
            throw new AppException(exception.Code);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
            exception is HttpRequestException or System.Text.Json.JsonException or OperationCanceledException)
        {
            throw new AppException("ImportSource", 502);
        }
    }
}
