using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Infrastructure.Identity;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Api.Tests;

[Collection("postgres")]
public sealed class ApiKeyModuleApiTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task ExistingKeyRecordsAndBothTransportsKeepTheirOwnerAndRevocationAsync()
    {
        using var alice = await fixture.CreateOwnerAsync(); using var bob = await fixture.CreateOwnerAsync();
        var project = await alice.PostAsync<string>("projects", new ProjectModel { Name = "Alice only" });
        await bob.PostAsync<string>("projects", new ProjectModel { Name = "Bob only" });
        var created = await alice.PostAsync<CreatedApiToken>("tokens", new CreateApiKeyRequest("  Agent  "));
        var rawKey = "whid_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var persistedId = Guid.NewGuid().ToString("N");
        await using (var scope = fixture.App.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
            await using var write = store.LightweightSession();
            write.Store(new ApiToken { Id = persistedId, UserId = alice.Owner, Name = "Existing format", Suffix = rawKey[^6..],
                Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))), CreatedAt = fixture.Clock.GetUtcNow() });
            await write.SaveChangesAsync();
            await using var read = store.QuerySession();
            var stored = (await read.LoadAsync<ApiToken>(created.Id))!;
            Assert.Equal("Agent", stored.Name); Assert.Equal(created.Token[^6..], stored.Suffix);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(created.Token))), stored.Hash);
            Assert.NotEqual(created.Token, stored.Hash);
        }
        using var external = fixture.App.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        foreach (var credential in new[] { new AuthenticationHeaderValue("Bearer", rawKey),
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(rawKey + ":api_token"))) })
        {
            external.DefaultRequestHeaders.Authorization = credential;
            var projects = await external.GetFromJsonAsync<List<ProjectModel>>("/api/v1/projects");
            Assert.Equal(project, Assert.Single(projects!).Id);
        }
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/projects"))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rawKey);
            using var response = await bob.Http.SendAsync(request);
            Assert.Equal(project, Assert.Single((await response.Content.ReadFromJsonAsync<List<ProjectModel>>())!).Id);
        }
        foreach (var headers in new[] { new[] { "Bearer wrong" }, new[] { "Basic !!!" },
            new[] { "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(rawKey + ":wrong")) },
            new[] { "Bearer " + rawKey, "Bearer wrong" } })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/projects");
            request.Headers.TryAddWithoutValidation("Authorization", headers);
            using var response = await bob.Http.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Http.DeleteAsync("/api/v1/tokens/" + persistedId)).StatusCode);
        var list = await alice.GetAsync<List<ApiTokenModel>>("tokens");
        Assert.Contains(list, key => key.Id == persistedId); Assert.DoesNotContain(rawKey, await alice.Http.GetStringAsync("/api/v1/tokens"));
        (await alice.Http.DeleteAsync("/api/v1/tokens/" + persistedId)).EnsureSuccessStatusCode();
        (await alice.Http.DeleteAsync("/api/v1/tokens/" + persistedId)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await external.GetAsync("/api/v1/projects")).StatusCode);
        Assert.Equal(project, Assert.Single(await alice.GetAsync<List<ProjectModel>>("projects")).Id);
        external.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", created.Token);
        await using (var scope = fixture.App.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            Assert.True((await users.DeleteAsync((await users.FindByIdAsync(alice.Owner))!)).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await external.GetAsync("/api/v1/projects")).StatusCode);
    }
}
