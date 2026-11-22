using Shared.Infrastructure.Authentication.ApiKey;
using FastEndpoints.Mcp;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace WhatHaveIDone.API.Mcp;

public static class TrackingMcpEndpoints
{
    private const string PolicyName = "McpApiKey";

    public static void AddTrackingMcp(this IServiceCollection services)
    {
        services.AddAuthorization(options => options.AddPolicy(PolicyName, policy => policy
            .AddAuthenticationSchemes(ApiKeyAuthenticationDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(context => context.Resource is HttpContext http &&
                http.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))));
        services.AddMcp();
        services.Configure<McpServerOptions>(options =>
        {
            options.ServerInfo = new Implementation { Name = "WhatHaveIDone", Version = "1.0.0" };
            options.ServerInstructions = "Personal time tracking for the authenticated API key owner. Read get_settings for the user's time zone. " +
                "Use ISO 8601 instants with Z or an explicit UTC offset. Entries are confirmed work, not drafts. " +
                "Review proposed writes with the user. Projects and descriptions are optional; tags are created implicitly when used. " +
                "Reuse createRequestId when retrying a creation. Read the latest entry version before updating or deleting.";
        });
    }

    public static void MapTrackingMcp(this WebApplication app)
    {
        app.UseMcp("/mcp", route => route.RequireAuthorization(PolicyName));
        // Unsupported MCP methods/paths must not fall through to the WASM navigation fallback.
        app.Map("/mcp/{**path}", IResult (HttpContext http) =>
        {
            if (!http.Request.Path.Equals("/mcp")) return Results.NotFound();
            http.Response.Headers.Allow = "POST";
            return Results.StatusCode(StatusCodes.Status405MethodNotAllowed);
        }).RequireAuthorization(PolicyName);
    }
}
