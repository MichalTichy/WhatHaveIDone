using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Infrastructure.Authentication.ApiKey;
using Shared.Infrastructure.CurrentUserProvider;

namespace WhatHaveIDone.Api.Tests;

public sealed class SharedApiKeyModuleTests
{
    [Fact]
    public async Task CoreManagementWorksWithAnInjectedStorageWithoutMartenAsync()
    {
        var storage = new MemoryKeys(); var actor = new Actor { UserId = "alice" }; var clock = new Clock();
        var services = new ServiceCollection();
        services.AddLogging(); services.AddSingleton<IApiKeyStorageService>(storage);
        services.AddSingleton<ICurrentUserIdProvider>(actor); services.AddSingleton<TimeProvider>(clock);
        services.AddAuthentication().AddApiKeys<IdentityUser>(options => options.KeyPrefix = "other_");
        using var provider = services.BuildServiceProvider();
        var manager = provider.GetRequiredService<ApiKeyManager>();
        var first = await manager.CreateAsync("  Agent  "); var second = await manager.CreateAsync("Another");
        Assert.StartsWith("other_", first.Key); Assert.NotEqual(first.Key, second.Key);
        var stored = storage.Keys[first.Id];
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(first.Key))), stored.Hash);
        Assert.NotEqual(first.Key, stored.Hash); Assert.Equal(first.Key[^6..], stored.Suffix); Assert.Equal("Agent", stored.Name);
        Assert.Equal(clock.Now, stored.CreatedAt); Assert.Equal("alice", stored.UserId);
        var metadata = JsonSerializer.Serialize(await manager.ListAsync());
        Assert.DoesNotContain(first.Key, metadata); Assert.DoesNotContain(stored.Hash, metadata);
        actor.UserId = "bob";
        Assert.Empty(await manager.ListAsync()); Assert.False(await manager.RevokeAsync(first.Id)); Assert.Null(stored.RevokedAt);
        actor.UserId = "alice";
        Assert.True(await manager.RevokeAsync(first.Id)); var revoked = stored.RevokedAt;
        clock.Now = clock.Now.AddHours(1);
        Assert.True(await manager.RevokeAsync(first.Id)); Assert.Equal(revoked, stored.RevokedAt);
        Assert.Null(await storage.FindActiveByHashAsync(stored.Hash, CancellationToken.None));
        Assert.Equal(2, (await manager.ListAsync()).Count);
        await Assert.ThrowsAsync<ArgumentException>(() => manager.CreateAsync(" "));
        await Assert.ThrowsAsync<ArgumentException>(() => manager.CreateAsync(new string('x', 101)));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.CreateAsync("Cancelled", cancelled.Token));
        Assert.Equal(2, storage.Keys.Count);
        actor.UserId = null;
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.CreateAsync("Anonymous"));
        Assert.Equal(2, storage.Keys.Count);
    }

    [Theory]
    [InlineData("Authorization")]
    [InlineData("X-Api-Key")]
    public async Task SharedHandlerUsesItsValidatorAndFailsClosedForMalformedCredentialsAsync(string headerName)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        var validator = new Validator();
        builder.Services.AddSingleton<IApiKeyValidator>(validator);
        builder.Services.AddAuthorization();
        builder.Services.AddAuthentication("TestKey")
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("TestKey", options => { options.HeaderName = headerName; options.KeyPrefix = "valid-"; });
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization();
        app.MapGet("/", (HttpContext context) => context.User.Identity!.AuthenticationType + ":" +
            context.User.FindFirstValue(ClaimTypes.NameIdentifier)).RequireAuthorization();
        await app.StartAsync();
        using var client = app.GetTestClient();
        using (var valid = new HttpRequestMessage(HttpMethod.Get, "/"))
        {
            valid.Headers.TryAddWithoutValidation(headerName, headerName == "Authorization" ? "bEaReR valid-key" : "valid-key");
            using var response = await client.SendAsync(valid);
            response.EnsureSuccessStatusCode(); Assert.Equal("TestKey:shared-subject", await response.Content.ReadAsStringAsync());
        }
        string[][] invalid = [[], ["Bearer"], ["Bearer invalid"], ["Bearer VALID-key"], ["Bearer valid-key extra"], ["Unknown valid-key"],
            ["Basic dmFsaWQta2V5OmFwaV90b2tlbg=="], ["Bearer " + new string('x', 257)],
            ["Bearer " + new string('x', 2050)], ["Bearer valid-key", "Bearer invalid"]];
        if (headerName != "Authorization") invalid = [[], ["invalid"], ["VALID-key"], ["valid-key extra"],
            ["Bearer valid-key"], ["valid-" + new string('x', 257)], ["valid-key", "valid-key"]];
        foreach (var headers in invalid)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/");
            if (headers.Length > 0) request.Headers.TryAddWithoutValidation(headerName, headers);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
            Assert.DoesNotContain("valid-key", await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(1, validator.Calls);
        // Matching prefixes still require validation against stored credentials.
        using var unknown = new HttpRequestMessage(HttpMethod.Get, "/");
        unknown.Headers.TryAddWithoutValidation(headerName, headerName == "Authorization" ? "Bearer valid-unknown" : "valid-unknown");
        using var rejected = await client.SendAsync(unknown);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.Equal(2, validator.Calls);
        if (headerName != "Authorization")
        {
            using var wrongHeader = new HttpRequestMessage(HttpMethod.Get, "/");
            wrongHeader.Headers.TryAddWithoutValidation("Authorization", "Bearer valid-key");
            using var missing = await client.SendAsync(wrongHeader);
            Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
            Assert.Equal(2, validator.Calls);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("Invalid Header")]
    [InlineData("Header:Name")]
    public void RegistrationRejectsInvalidHeaderNames(string headerName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication().AddApiKeys<IdentityUser>(options => options.HeaderName = headerName);
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptionsMonitor<ApiKeyAuthenticationOptions>>()
            .Get(ApiKeyAuthenticationDefaults.AuthenticationScheme));
    }

    private sealed class Actor : ICurrentUserIdProvider
    {
        public string? UserId { get; set; }
        public Task<string?> GetCurrentUserIdAsync() => Task.FromResult(UserId);
        public Task<string> GetCurrentUserIdRequiredAsync() => Task.FromResult(UserId ?? throw new InvalidOperationException("Unauthenticated"));
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-11-18T10:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Validator : IApiKeyValidator
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyCollection<Claim>?> ValidateAsync(string key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult<IReadOnlyCollection<Claim>?>(key == "valid-key" ? [new Claim(ClaimTypes.NameIdentifier, "shared-subject")] : null);
        }
    }
    private sealed class MemoryKeys : IApiKeyStorageService
    {
        public Dictionary<string, ApiKeyRecord> Keys { get; } = [];
        public Task AddAsync(ApiKeyRecord key, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); Keys.Add(key.Id, key); return Task.CompletedTask; }
        public Task<ApiKeyRecord?> FindActiveByHashAsync(string hash, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Keys.Values.SingleOrDefault(key => key.Hash == hash && key.RevokedAt is null)); }
        public Task<IReadOnlyList<ApiKeyInfo>> ListAsync(string userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ApiKeyInfo>>(Keys.Values.Where(key => key.UserId == userId).OrderByDescending(key => key.CreatedAt)
                .Select(key => new ApiKeyInfo(key.Id, key.Name, key.Suffix, key.CreatedAt, key.RevokedAt)).ToList());
        }
        public Task<bool> RevokeAsync(string id, string userId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Keys.TryGetValue(id, out var key) || key.UserId != userId) return Task.FromResult(false);
            key.RevokedAt ??= revokedAt; return Task.FromResult(true);
        }
    }
}
