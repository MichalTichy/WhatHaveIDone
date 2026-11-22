namespace WhatHaveIDone.API.Mcp;

internal static class McpInvocation
{
    // FastEndpoints.Mcp supplies endpoint metadata without an ASP.NET route match.
    public static bool IsTool(HttpContext http) => http.GetEndpoint() is { } endpoint && endpoint is not RouteEndpoint;
}
