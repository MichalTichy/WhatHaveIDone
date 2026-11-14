using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using WhatHaveIDone.API.Models;

namespace WhatHaveIDone.Api.Tests;

[Collection("postgres")]
public sealed class ApiProtocolTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task NativeAndTogglAdaptersKeepTheirErrorContractsAsync()
    {
        using var anonymous = fixture.App.CreateClient();
        await AssertErrorAsync(anonymous, "/api/v1/projects", HttpStatusCode.Unauthorized, JsonValueKind.Object);
        await AssertErrorAsync(anonymous, "/api/v9/workspaces", HttpStatusCode.Unauthorized, JsonValueKind.String);

        using var owner = await fixture.CreateOwnerAsync();
        var token = await owner.PostAsync<CreatedApiToken>("tokens", new CreateApiKeyRequest("Protocol contracts"));
        using var toggl = fixture.App.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        toggl.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(token.Token + ":api_token")));

        await AssertErrorAsync(owner.Http, "/api/v1/time-entries?from=2026-10-27T00:00:00Z&to=2026-10-26T00:00:00Z", HttpStatusCode.BadRequest, JsonValueKind.Object);
        await AssertErrorAsync(toggl, "/api/v9/me/time_entries?start_date=2026-10-27T00:00:00Z&end_date=2026-10-26T00:00:00Z", HttpStatusCode.BadRequest, JsonValueKind.String);
        await AssertErrorAsync(owner.Http, "/api/v1/not-an-endpoint", HttpStatusCode.NotFound, JsonValueKind.Object);
        await AssertErrorAsync(toggl, "/api/v9/not-an-endpoint", HttpStatusCode.NotFound, JsonValueKind.String);
    }

    private static async Task AssertErrorAsync(HttpClient client, string path, HttpStatusCode status, JsonValueKind shape)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(status, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(shape, body.RootElement.ValueKind);
        if (shape == JsonValueKind.Object)
        {
            Assert.Equal(JsonValueKind.String, body.RootElement.GetProperty("code").ValueKind);
            Assert.Equal(JsonValueKind.String, body.RootElement.GetProperty("message").ValueKind);
        }
    }
}
