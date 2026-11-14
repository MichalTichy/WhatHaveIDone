using System.Net.Http.Json;
using System.Text.Json;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Web.Resources;

namespace WhatHaveIDone.Web.Services;

public sealed class ApiClient(HttpClient http)
{
    public string? CsrfToken { get; set; }
    public HttpClient Http => http;
    public async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        try { using var response = await http.GetAsync("api/v1/" + path, cancellationToken); return await ReadAsync<T>(response, cancellationToken); }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested) { throw new HttpRequestException(Strings.Offline, exception); }
    }
    public async Task<T> SendAsync<T>(HttpMethod method, string path, object? value, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, "api/v1/" + path);
        if (value is HttpContent content) request.Content = content;
        else if (value is not null) request.Content = JsonContent.Create(value, value.GetType());
        if (CsrfToken is not null) request.Headers.Add("X-CSRF-TOKEN", CsrfToken);
        try { using var response = await http.SendAsync(request, cancellationToken); return await ReadAsync<T>(response, cancellationToken); }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested) { throw new HttpRequestException(Strings.Offline, exception); }
    }
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            ApiError? error = null;
            try { error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken); } catch (JsonException) { }
            throw new ApiCallException(error?.Code ?? "UnexpectedError", error?.Message ?? Strings.UnexpectedError, (int)response.StatusCode);
        }
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return default!;
        return (await response.Content.ReadFromJsonAsync<T>(cancellationToken))!;
    }
}
