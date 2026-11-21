using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Http;
using Shared.Infrastructure.Authentication.ApiKey;

namespace WhatHaveIDone.API.Toggl;

public static class TogglApiKeyAuthentication
{
    public static void UseTogglCompatibility(this ApiKeyAuthenticationOptions options)
    {
        options.ReadAdditionalKey = ReadKey;
        options.AdditionalChallenge = request => request.Path.StartsWithSegments(TogglEndpoints.ApiPath)
            ? "Basic realm=\"WhatHaveIDone\"" : null;
    }

    private static string? ReadKey(HttpRequest request)
    {
        if (!AuthenticationHeaderValue.TryParse(request.Headers.Authorization.ToString(), out var authorization) ||
            !authorization.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase) || authorization.Parameter is null) return null;
        try
        {
            var pair = Encoding.UTF8.GetString(Convert.FromBase64String(authorization.Parameter)).Split(':', 2);
            return pair.Length == 2 && pair[1] == "api_token" ? pair[0] : null;
        }
        catch (FormatException) { return null; }
    }
}
