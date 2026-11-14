using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;
using WhatHaveIDone.API.Models;

namespace WhatHaveIDone.Api.Tests;

[Collection("postgres")]
public sealed class McpApiTests(IntegrationFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string From = "2026-10-25T00:00:00Z", To = "2026-10-27T00:00:00Z";

    [Fact]
    public async Task DiscoveryRequiresAnActiveBearerKeyAndRejectsCrossOriginAsync()
    {
        using var anonymous = fixture.App.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var unauthenticated = await anonymous.SendAsync(Request("initialize", Initialize));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        Assert.Equal("Bearer", Assert.Single(unauthenticated.Headers.WwwAuthenticate).Scheme);
        Assert.True(unauthenticated.Headers.CacheControl?.NoStore);
        Assert.Null(unauthenticated.Headers.Location);
        using var anonymousGet = await anonymous.GetAsync("/mcp");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousGet.StatusCode);
        using var owner = await fixture.CreateOwnerAsync();
        using var cookieOnly = await owner.Http.SendAsync(Request("tools/list", new { }));
        Assert.Equal(HttpStatusCode.Unauthorized, cookieOnly.StatusCode);

        var key = await owner.PostAsync<CreatedApiToken>("tokens", new CreateApiKeyRequest("MCP test"));
        using var client = Client(key.Token);
        var initialized = await RpcAsync(client, "initialize", Initialize);
        using var unsupportedGet = await client.GetAsync("/mcp");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, unsupportedGet.StatusCode);
        Assert.Contains("POST", unsupportedGet.Content.Headers.Allow);
        using var unknownPath = await client.GetAsync("/mcp/not-an-endpoint");
        Assert.Equal(HttpStatusCode.NotFound, unknownPath.StatusCode);
        Assert.NotEqual("text/html", unknownPath.Content.Headers.ContentType?.MediaType);
        Assert.Equal("WhatHaveIDone", initialized.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
        var listed = await RpcAsync(client, "tools/list", new { });
        var tools = listed.GetProperty("result").GetProperty("tools").EnumerateArray().ToList();
        Assert.Contains(tools, t => t.GetProperty("name").GetString() == "create_time_entry");
        Assert.Contains(tools, t => t.GetProperty("name").GetString() == "get_report" && t.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
        Assert.DoesNotContain(tools, t => t.GetProperty("name").GetString()!.Contains("token", StringComparison.Ordinal));
        Assert.DoesNotContain(key.Token, listed.GetRawText());
        Assert.DoesNotContain("actor", listed.GetRawText());
        using var originRequest = Request("tools/list", new { });
        originRequest.Headers.Add("Origin", "https://untrusted.example");
        using var deniedOrigin = await client.SendAsync(originRequest);
        Assert.Equal(HttpStatusCode.Forbidden, deniedOrigin.StatusCode);
        using var sameOrigin = Request("tools/list", new { });
        sameOrigin.Headers.Add("Origin", client.BaseAddress!.GetLeftPart(UriPartial.Authority));
        using var allowedOrigin = await client.SendAsync(sameOrigin);
        allowedOrigin.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(key.Token + ":api_token")));
        using var basic = await client.SendAsync(Request("tools/list", new { }));
        Assert.Equal(HttpStatusCode.Forbidden, basic.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key.Token);
        using var revoked = await owner.Http.DeleteAsync("/api/v1/tokens/" + key.Id);
        revoked.EnsureSuccessStatusCode();
        using var afterRevocation = await client.SendAsync(Request("tools/list", new { }));
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevocation.StatusCode);
    }

    [Fact]
    public async Task BothProtocolGenerationsUseStatelessAuthenticatedRequestsAsync()
    {
        using var owner = await fixture.CreateOwnerAsync();
        var key = await owner.PostAsync<CreatedApiToken>("tokens", new CreateApiKeyRequest("MCP protocols"));
        using var client = Client(key.Token);
        var discovery = await RpcAsync(client, "server/discover", new { }, "2026-07-28");
        Assert.True(discovery.TryGetProperty("result", out _));
        using var request = Request("tools/list", new { }, "2026-07-28");
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.False(response.Headers.Contains("Mcp-Session-Id"));
        var legacy = await RpcAsync(client, "tools/list", new { });
        Assert.NotEmpty(legacy.GetProperty("result").GetProperty("tools").EnumerateArray());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var sdk = await McpClient.CreateAsync(new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(client.BaseAddress!, "/mcp") }, client), cancellationToken: timeout.Token);
        Assert.Contains(await sdk.ListToolsAsync(cancellationToken: timeout.Token), tool => tool.Name == "get_settings");
        var settings = await sdk.CallToolAsync("get_settings", cancellationToken: timeout.Token);
        Assert.NotEqual(true, settings.IsError);
        Assert.NotNull(settings.StructuredContent);
    }

    [Fact]
    public async Task ToolsUseOwnerScopedReadsAndRepeatableEntryCreationAsync()
    {
        using var alice = await fixture.CreateOwnerAsync();
        using var bob = await fixture.CreateOwnerAsync();
        var project = await alice.PostAsync<string>("projects", new ProjectModel { Name = "Alice project", Color = "#7c3aed" });
        var keyA = await alice.PostAsync<CreatedApiToken>("tokens", new CreateApiKeyRequest("Alice MCP"));
        var keyB = await bob.PostAsync<CreatedApiToken>("tokens", new CreateApiKeyRequest("Bob MCP"));
        using var a = Client(keyA.Token); using var b = Client(keyB.Token);
        var input = new { createRequestId = Guid.NewGuid(), start = "2026-10-26T09:00:00+01:00", end = "2026-10-26T10:00:00+01:00", description = "  Private work  ", projectId = project, tags = new[] { " Review ", "review", "", " Development " } };
        var first = await CallAsync<TimeEntryModel>(a, "create_time_entry", input);
        var repeated = await CallAsync<TimeEntryModel>(a, "create_time_entry", input);
        Assert.Equal(first.Id, repeated.Id);
        Assert.Equal("Private work", first.Description);
        Assert.Equal(["Review", "Development"], first.Tags);
        Assert.Equal(DateTimeOffset.Parse("2026-10-26T08:00:00Z"), first.Start);
        Assert.Single(await CallAsync<List<TimeEntryModel>>(a, "list_time_entries", new { from = From, to = To }));
        Assert.Empty(await CallAsync<List<TimeEntryModel>>(b, "list_time_entries", new { from = From, to = To }));
        Assert.Single(await CallAsync<List<ProjectModel>>(a, "list_projects", new { }));
        Assert.Empty(await CallAsync<List<ProjectModel>>(b, "list_projects", new { }));
        Assert.Equal("Review", Assert.Single(await CallAsync<List<string>>(a, "list_tags", new { search = " REVIEW " })));
        Assert.Empty(await CallAsync<List<string>>(b, "list_tags", new { }));
        await AssertToolErrorAsync(b, "get_time_entry", new { id = first.Id }, "NotFound");
        await AssertToolErrorAsync(b, "create_time_entry", input with { createRequestId = Guid.NewGuid() }, "NotFound");
        Assert.Equal(first.Id, (await alice.GetAsync<TimeEntryModel>("time-entries/" + Uri.EscapeDataString(first.Id))).Id);
        var noTags = await CallAsync<TimeEntryModel>(a, "create_time_entry", new { createRequestId = Guid.NewGuid(), start = "2026-10-26T10:00:00Z", end = "2026-10-26T10:30:00Z" });
        Assert.Empty(noTags.Tags); Assert.Null(noTags.ProjectId); Assert.Empty(noTags.Description);
    }

    [Fact]
    public async Task EditsKeepVersionChecksOwnershipAndSafeMcpErrorsAsync()
    {
        using var alice = await fixture.CreateOwnerAsync(); using var bob = await fixture.CreateOwnerAsync();
        var key = await alice.PostAsync<CreatedApiToken>("tokens", new CreateApiKeyRequest("MCP edits"));
        var otherKey = await bob.PostAsync<CreatedApiToken>("tokens", new CreateApiKeyRequest("Other MCP"));
        using var client = Client(key.Token); using var other = Client(otherKey.Token);
        var entry = await CallAsync<TimeEntryModel>(client, "create_time_entry", new { createRequestId = Guid.NewGuid(), start = "2026-10-26T08:00:00Z", end = "2026-10-26T09:00:00Z" });
        var update = new { id = entry.Id, version = entry.Version, start = entry.Start, end = entry.End, description = "Updated", projectId = (string?)null, tags = new[] { "  Focus  " } };
        await AssertToolErrorAsync(other, "update_time_entry", update, "NotFound");
        await AssertToolErrorAsync(other, "delete_time_entry", new { id = entry.Id, version = entry.Version }, "NotFound");
        var updated = await CallAsync<TimeEntryModel>(client, "update_time_entry", update);
        Assert.NotEqual(entry.Version, updated.Version); Assert.Equal(["Focus"], updated.Tags);
        await AssertToolErrorAsync(client, "update_time_entry", update, "Conflict");
        await AssertToolErrorAsync(client, "delete_time_entry", new { id = entry.Id, version = entry.Version }, "Conflict");
        await AssertToolErrorAsync(client, "create_time_entry", new { createRequestId = Guid.Empty, start = entry.Start, end = entry.End }, "InvalidRequest");
        await AssertToolErrorAsync(client, "list_time_entries", new { from = To, to = From }, "InvalidInterval");
        await AssertToolErrorAsync(client, "create_time_entry", new { createRequestId = Guid.NewGuid(), start = entry.End, end = entry.Start }, "InvalidInterval");
        var deleted = await RpcAsync(client, "tools/call", new { name = "delete_time_entry", arguments = new { id = updated.Id, version = updated.Version } });
        Assert.False(IsToolError(deleted));
        await AssertToolErrorAsync(client, "get_time_entry", new { id = entry.Id }, "NotFound");
    }

    [Fact]
    public async Task TimerToolsSwitchAtomicallyAndStopOnlyTheSpecifiedOwnerTimerAsync()
    {
        using var owner = await fixture.CreateOwnerAsync(); using var otherOwner = await fixture.CreateOwnerAsync();
        var key = await owner.PostAsync<CreatedApiToken>("tokens", new CreateApiKeyRequest("MCP timer"));
        var otherKey = await otherOwner.PostAsync<CreatedApiToken>("tokens", new CreateApiKeyRequest("Other timer"));
        using var client = Client(key.Token); using var other = Client(otherKey.Token);
        var first = await CallAsync<TimeEntryModel>(client, "start_timer", new { description = "First", tags = new[] { " Task ", "task" } });
        var second = await CallAsync<TimeEntryModel>(client, "start_timer", new { description = "Second" });
        Assert.NotNull((await owner.GetAsync<TimeEntryModel>("time-entries/" + first.Id)).End);
        Assert.Equal(second.Id, (await CallAsync<TimeEntryModel>(client, "get_timer", new { })).Id);
        Assert.Single(await owner.GetAsync<List<TimeEntryModel>>("time-entries?from=" + From + "&to=" + To), e => e.End is null);
        await AssertToolErrorAsync(other, "stop_timer", new { id = second.Id }, "NotFound");
        await CallAsync<TimeEntryModel>(client, "stop_timer", new { id = first.Id });
        Assert.Equal(second.Id, (await CallAsync<TimeEntryModel>(client, "get_timer", new { })).Id);
        var stopped = await CallAsync<TimeEntryModel>(client, "stop_timer", new { id = second.Id });
        var retried = await CallAsync<TimeEntryModel>(client, "stop_timer", new { id = second.Id });
        Assert.NotNull(stopped.End); Assert.Equal(stopped.End, retried.End);
        Assert.Null(await CallAsync<TimeEntryModel?>(client, "get_timer", new { }));
    }

    [Fact]
    public async Task ReportsAndSettingsMatchTheNativeApiAcrossDstAsync()
    {
        using var owner = await fixture.CreateOwnerAsync();
        var settings = await owner.GetAsync<SettingsModel>("settings");
        await owner.PutAsync<bool>("settings", settings with { TimeZone = "Europe/Prague" });
        var key = await owner.PostAsync<CreatedApiToken>("tokens", new CreateApiKeyRequest("MCP reports"));
        using var client = Client(key.Token);
        await CallAsync<TimeEntryModel>(client, "create_time_entry", new { createRequestId = Guid.NewGuid(), start = "2026-10-25T02:30:00+02:00", end = "2026-10-25T02:30:00+01:00", tags = new[] { "DST" } });
        var profile = await CallAsync<SettingsModel>(client, "get_settings", new { });
        Assert.Equal("Europe/Prague", profile.TimeZone);
        var report = await CallAsync<ReportModel>(client, "get_report", new { from = "2026-10-25T00:45:00Z", to = "2026-10-25T01:15:00Z", tag = "dst" });
        var native = await owner.GetAsync<ReportModel>("reports?from=2026-10-25T00:45:00Z&to=2026-10-25T01:15:00Z&tag=dst");
        Assert.Equal(1800, report.TotalSeconds);
        Assert.Equal(JsonSerializer.Serialize(native, Json), JsonSerializer.Serialize(report, Json));
    }

    private static object Initialize => new { protocolVersion = "2025-11-25", capabilities = new { }, clientInfo = new { name = "Contract tests", version = "1.0" } };
    private HttpClient Client(string token)
    {
        var client = fixture.App.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
    private static HttpRequestMessage Request(string method, object parameters, string protocol = "2025-11-25")
    {
        var args = JsonSerializer.SerializeToNode(parameters)!.AsObject();
        if (protocol == "2026-07-28") args["_meta"] = new JsonObject
        {
            ["io.modelcontextprotocol/protocolVersion"] = protocol,
            ["io.modelcontextprotocol/clientCapabilities"] = new JsonObject(),
            ["io.modelcontextprotocol/clientInfo"] = new JsonObject { ["name"] = "Contract tests", ["version"] = "1.0" }
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params = args }) };
        request.Headers.Accept.ParseAdd("application/json, text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", protocol);
        if (protocol == "2026-07-28")
        {
            request.Headers.Add("Mcp-Method", method);
            if (args["name"] is { } name) request.Headers.Add("Mcp-Name", name.GetValue<string>());
        }
        return request;
    }
    private static async Task<JsonElement> RpcAsync(HttpClient client, string method, object parameters, string protocol = "2025-11-25")
    {
        using var request = Request(method, parameters, protocol);
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {text}");
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
            text = text.Split('\n').First(line => line.StartsWith("data: ", StringComparison.Ordinal))[6..];
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }
    private static bool IsToolError(JsonElement response) => response.TryGetProperty("error", out _) ||
        response.GetProperty("result").TryGetProperty("isError", out var error) && error.GetBoolean();
    private static async Task<T> CallAsync<T>(HttpClient client, string name, object arguments)
    {
        var response = await RpcAsync(client, "tools/call", new { name, arguments });
        Assert.False(IsToolError(response), response.GetRawText());
        var result = response.GetProperty("result");
        if (result.TryGetProperty("structuredContent", out var value))
        {
            if (value.TryGetProperty("result", out var wrapped)) value = wrapped;
            return value.Deserialize<T>(Json)!;
        }
        var content = result.GetProperty("content");
        return content.GetArrayLength() == 0 ? default! : JsonSerializer.Deserialize<T>(content[0].GetProperty("text").GetString()!, Json)!;
    }
    private static async Task AssertToolErrorAsync(HttpClient client, string name, object arguments, string code)
    {
        var response = await RpcAsync(client, "tools/call", new { name, arguments });
        Assert.True(IsToolError(response), response.GetRawText());
        Assert.Contains(code, response.GetRawText());
        Assert.DoesNotContain("Npgsql", response.GetRawText());
        Assert.DoesNotContain("StackTrace", response.GetRawText());
    }
}
