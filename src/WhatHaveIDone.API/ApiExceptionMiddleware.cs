using JasperFx;
using Npgsql;
using WhatHaveIDone.Contracts;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Resources;

namespace WhatHaveIDone.API;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
            if (context.Request.Path.StartsWithSegments("/api") && context.Response.StatusCode >= 400 && !context.Response.HasStarted && string.IsNullOrEmpty(context.Response.ContentType))
            {
                var code=context.Response.StatusCode switch {401=>"Unauthenticated",403=>"SsoDenied",404=>"NotFound",429=>"TooManyRequests",_=>"InvalidRequest"};
                var message=Strings.ResourceManager.GetString(code)??Strings.UnexpectedError;
                context.Response.Headers.CacheControl="no-store";
                await context.Response.WriteAsJsonAsync(new ApiError(code,message),context.RequestAborted);
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { context.Abort(); }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var error = exception as AppException;
            if (exception is ConcurrencyException || exception is PostgresException { SqlState: "23505" or "40001" }) error = new AppException("Conflict", 409);
            if (exception is BadHttpRequestException) error = new AppException("InvalidRequest");
            if (exception is FormatException) error = new AppException("InvalidInterval");
            if (error is null) logger.LogError(exception, "Request failed: {Path}", context.Request.Path);
            context.Response.StatusCode = error?.Status ?? 500;
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(new ApiError(error?.Code ?? "UnexpectedError", error?.MessageForUser ?? Strings.UnexpectedError), context.RequestAborted);
        }
    }
}
