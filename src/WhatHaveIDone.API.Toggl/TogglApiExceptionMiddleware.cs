using JasperFx;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Npgsql;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Resources;

namespace WhatHaveIDone.API.Toggl;

public sealed class TogglApiExceptionMiddleware(RequestDelegate next, ILogger<TogglApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(TogglEndpoints.ApiPath))
        {
            await next(context);
            return;
        }

        try
        {
            await next(context);
            if (context.Response.StatusCode >= 400 && !context.Response.HasStarted && string.IsNullOrEmpty(context.Response.ContentType))
            {
                var code = context.Response.StatusCode switch
                {
                    401 => "Unauthenticated",
                    403 => "SsoDenied",
                    404 => "NotFound",
                    429 => "TooManyRequests",
                    _ => "InvalidRequest"
                };
                context.Response.Headers.CacheControl = "no-store";
                await context.Response.WriteAsJsonAsync(Strings.ResourceManager.GetString(code) ?? Strings.UnexpectedError, context.RequestAborted);
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Abort();
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var error = exception as AppException;
            if (exception is ConcurrencyException || exception is PostgresException { SqlState: "23505" or "40001" }) error = new AppException("Conflict", 409);
            if (exception is BadHttpRequestException) error = new AppException("InvalidRequest");
            if (exception is FormatException) error = new AppException("InvalidInterval");
            if (error is null) logger.LogError(exception, "Toggl API request failed: {Path}", context.Request.Path);
            context.Response.StatusCode = error?.Status ?? 500;
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(error?.MessageForUser ?? Strings.UnexpectedError, context.RequestAborted);
        }
    }
}
