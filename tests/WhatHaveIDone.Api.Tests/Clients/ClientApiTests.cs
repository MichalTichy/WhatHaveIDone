using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WhatHaveIDone.API.Models;

namespace WhatHaveIDone.Api.Tests;

[Collection("postgres")]
public sealed class ClientApiTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task ClientsHaveOwnedStableIdentitiesAndProjectsUseTheSelectedOrDefaultClientAsync()
    {
        using var alice = await fixture.CreateOwnerAsync();
        using var bob = await fixture.CreateOwnerAsync();
        var personal = Assert.Single(await alice.GetAsync<List<ClientModel>>("clients"));
        Assert.True(personal.IsDefault);
        var otherDefault = Assert.Single(await bob.GetAsync<List<ClientModel>>("clients"));
        Assert.NotEqual(personal.Id, otherDefault.Id);
        await alice.PutAsync<string>("clients/" + Uri.EscapeDataString(personal.Id), personal with { Name = "  My work  " });
        var renamed = Assert.Single(await alice.GetAsync<List<ClientModel>>("clients"));
        Assert.Equal("My work", renamed.Name); Assert.Equal(personal.CompatibilityId, renamed.CompatibilityId); Assert.True(renamed.IsDefault);
        Assert.Equal(HttpStatusCode.Conflict, (await alice.Http.PutAsJsonAsync("/api/v1/clients/" + Uri.EscapeDataString(personal.Id), personal with { Name = "Stale" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Http.PutAsJsonAsync("/api/v1/clients/" + Uri.EscapeDataString(renamed.Id), renamed with { Name = "Foreign" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.Http.PostAsJsonAsync("/api/v1/clients", new ClientModel { Name = "  " })).StatusCode);
        var business = await alice.PostAsync<string>("clients", new ClientModel { Name = "  Business  ", IsDefault = true });
        var clients = await alice.GetAsync<List<ClientModel>>("clients");
        Assert.Single(clients, c => c.IsDefault); Assert.Equal("Business", clients.Single(c => c.Id == business).Name);
        var projectId = await alice.PostAsync<string>("projects", new ProjectModel { Name = "Selected", ClientId = business });
        await alice.PostAsync<string>("projects", new ProjectModel { Name = "Default" });
        var projects = await alice.GetAsync<List<ProjectModel>>("projects");
        Assert.Equal(personal.Id, projects.Single(p => p.Name == "Default").ClientId);
        var project = projects.Single(p => p.Id == projectId);
        Assert.Equal(business, project.ClientId);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Http.PostAsJsonAsync("/api/v1/projects", new ProjectModel { Name = "Foreign client", ClientId = business })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.Http.PutAsJsonAsync("/api/v1/projects/" + projectId, project with { ClientId = otherDefault.Id })).StatusCode);
        await alice.PutAsync<string>("projects/" + projectId, project with { ClientId = personal.Id });
        var togglClients = await alice.Http.GetFromJsonAsync<List<JsonElement>>("/api/v9/workspaces/ignored/clients");
        Assert.Equal(2, togglClients!.Count);
        Assert.Contains(togglClients, c => c.GetProperty("id").GetInt64() == personal.CompatibilityId && c.GetProperty("name").GetString() == "My work");
        var togglProject = (await alice.Http.GetFromJsonAsync<List<JsonElement>>("/api/v9/workspaces/999/projects"))!.Single(p => p.GetProperty("id").GetInt64() == project.CompatibilityId);
        Assert.Equal(personal.CompatibilityId, togglProject.GetProperty("cid").GetInt64());
        Assert.Equal(personal.CompatibilityId, togglProject.GetProperty("client_id").GetInt64());
        Assert.Single(await bob.GetAsync<List<ClientModel>>("clients"));
    }

}
