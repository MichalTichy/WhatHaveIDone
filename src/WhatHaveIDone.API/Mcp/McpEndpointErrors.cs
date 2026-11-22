using FastEndpoints;
using WhatHaveIDone.API.Models;

namespace WhatHaveIDone.API.Mcp;

internal sealed class McpEndpointErrors : IGlobalPostProcessor
{
    public async Task PostProcessAsync(IPostProcessorContext context, CancellationToken ct)
    {
        var http = context.HttpContext;
        if (!McpInvocation.IsTool(http) || http.Response.HasStarted) return;

        // The MCP bridge runs endpoints in-process, outside the HTTP error middleware.
        var error = ApiFailure.From(context.ExceptionDispatchInfo?.SourceException);
        if (error is null) return;

        http.Response.StatusCode = error.Status;
        await http.Response.WriteAsJsonAsync(new ApiError(error.Code, error.MessageForUser), ct);
        context.MarkExceptionAsHandled();
    }
}
