using JasperFx;
using ModelContextProtocol;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using Npgsql;
using WhatHaveIDone.API.Authentication;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Resources;

namespace WhatHaveIDone.API.Mcp;

public static class TrackingMcpEndpoints
{
    private const string PolicyName = "McpApiKey";

    public static void AddTrackingMcp(this IServiceCollection services)
    {
        services.AddAuthorization(options => options.AddPolicy(PolicyName, policy => policy
            .AddAuthenticationSchemes(ApiTokenHandler.SchemeName)
            .RequireAuthenticatedUser()
            .RequireAssertion(context => context.Resource is HttpContext http &&
                http.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))));
        services.AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation { Name = "WhatHaveIDone", Version = "1.0.0" };
            options.ServerInstructions = "Personal time tracking for the authenticated API key owner. Read get_settings for the user's time zone. " +
                "Use ISO 8601 instants with Z or an explicit UTC offset. Entries are confirmed work, not drafts. " +
                "Review proposed writes with the user. Projects and descriptions are optional; tags are created implicitly when used. " +
                "Reuse createRequestId when retrying a creation. Read the latest entry version before updating or deleting.";
        })
        .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
        .WithTools<TrackingMcpTools>()
        .WithRequestFilters(filters => filters.AddCallToolFilter(next => async (context, ct) =>
        {
            try { return await next(context, ct); }
            catch (AppException exception) { throw new McpException($"{exception.Code}: {exception.MessageForUser}"); }
            catch (Exception exception) when (exception is ConcurrencyException || exception is PostgresException { SqlState: "23505" or "40001" })
            { throw new McpException($"Conflict: {Strings.Conflict}"); }
        }));
    }

    public static void MapTrackingMcp(this WebApplication app)
    {
        app.MapMcp("/mcp").RequireAuthorization(PolicyName);
        // Unsupported MCP methods/paths must not fall through to the WASM navigation fallback.
        app.Map("/mcp/{**path}", IResult (HttpContext http) =>
        {
            if (!http.Request.Path.Equals("/mcp")) return Results.NotFound();
            http.Response.Headers.Allow = "POST";
            return Results.StatusCode(StatusCodes.Status405MethodNotAllowed);
        }).RequireAuthorization(PolicyName);
    }
}
