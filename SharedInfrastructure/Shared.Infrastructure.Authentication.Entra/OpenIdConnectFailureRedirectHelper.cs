using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Shared.Infrastructure.Authentication.Entra;

public static class OpenIdConnectFailureRedirectHelper
{
    public const string LoginFailedPath = "/login-failed";

    public static string BuildPathBaseAwareRedirectPath(string pathBase, string path)
    {
        var normalizedPathBase = $"/{pathBase.Trim('/')}";

        if (normalizedPathBase == "/")
        {
            return path.StartsWith("/", StringComparison.Ordinal)
                ? path
                : $"/{path}";
        }

        return path == "/"
            ? $"{normalizedPathBase}/"
            : $"{normalizedPathBase}/{path.TrimStart('/')}";
    }

    public static string ResolveRemoteFailureRedirectPath(
        Exception? exception,
        string loginFailedPath = LoginFailedPath,
        string correlationFailureRedirectPath = "/")
    {
        return IsCorrelationFailure(exception)
            ? correlationFailureRedirectPath
            : loginFailedPath;
    }

    public static Task HandleRemoteFailureAsync(
        RemoteFailureContext context,
        string loginFailedPath = LoginFailedPath,
        string correlationFailureRedirectPath = "/")
    {
        context.HandleResponse();

        var redirectPath = ResolveRemoteFailureRedirectPath(
            context.Failure,
            loginFailedPath,
            correlationFailureRedirectPath);
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(OpenIdConnectFailureRedirectHelper));

        logger.LogWarning(
            context.Failure,
            "OIDC remote failure on {RequestPath}. Redirecting to {RedirectPath}.",
            context.Request.Path,
            redirectPath);

        context.Response.Redirect(redirectPath);
        return Task.CompletedTask;
    }

    private static bool IsCorrelationFailure(Exception? exception)
    {
        while (exception is not null)
        {
            var message = exception.Message;

            if (message.Contains("Correlation failed", StringComparison.OrdinalIgnoreCase) ||
                message.Contains(".AspNetCore.Correlation", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("cookie not found", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            exception = exception.InnerException;
        }

        return false;
    }
}
