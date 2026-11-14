using System.Net;
using System.Net.Http.Json;
using WhatHaveIDone.API.Models;

namespace WhatHaveIDone.Api.Tests;

[Collection("postgres")]
public sealed class AccountApiTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task RegistrationLoginLogoutAndPasswordChangeKeepTheSameUsableAccountAsync()
    {
        using var owner = new OwnerClient(fixture.App.CreateClient(), "");
        await owner.RefreshCsrfAsync();
        var account = new AccountInput { Email = Guid.NewGuid().ToString("N") + "@example.test", Password = "Synthetic-password!123" };
        var id = await owner.PostAsync<string>("account/register", account);
        await owner.RefreshCsrfAsync();
        var session = await owner.GetAsync<SessionModel>("session");
        Assert.True(session.Authenticated); Assert.True(session.HasPassword);
        var client = Assert.Single(await owner.GetAsync<List<ClientModel>>("clients"));
        Assert.True(client.IsDefault);
        await owner.PostAsync<bool>("account/logout", null);
        await owner.RefreshCsrfAsync();
        Assert.False((await owner.GetAsync<SessionModel>("session")).Authenticated);

        using (var failed = await owner.Http.PostAsJsonAsync("/api/v1/account/login", new AccountInput { Email = account.Email, Password = "Wrong-password!123" }))
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        Assert.False((await owner.GetAsync<SessionModel>("session")).Authenticated);
        Assert.Equal(id, await owner.PostAsync<string>("account/login", account));
        await owner.RefreshCsrfAsync();
        using (var failed = await owner.Http.PostAsJsonAsync("/api/v1/account/password", new ChangePasswordInput("Wrong-password!123", "Changed-password!456")))
            Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
        await owner.PostAsync<bool>("account/password", new ChangePasswordInput(account.Password, "Changed-password!456"));
        await owner.RefreshCsrfAsync();
        Assert.True((await owner.GetAsync<SessionModel>("session")).Authenticated);
        await owner.PostAsync<bool>("account/logout", null);
        await owner.RefreshCsrfAsync();
        using (var oldPassword = await owner.Http.PostAsJsonAsync("/api/v1/account/login", account))
            Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        account.Password = "Changed-password!456";
        Assert.Equal(id, await owner.PostAsync<string>("account/login", account));
        Assert.Equal(client.Id, Assert.Single(await owner.GetAsync<List<ClientModel>>("clients")).Id);
    }

    [Fact]
    public async Task FailedPasswordAttemptsLockTheAccountAndDoNotIssueASessionAsync()
    {
        using var owner = new OwnerClient(fixture.App.CreateClient(), "");
        await owner.RefreshCsrfAsync();
        var account = new AccountInput { Email = Guid.NewGuid().ToString("N") + "@example.test", Password = "Synthetic-password!123" };
        await owner.PostAsync<string>("account/register", account);
        await owner.RefreshCsrfAsync();
        await owner.PostAsync<bool>("account/logout", null);
        await owner.RefreshCsrfAsync();
        for (var i = 0; i < 5; i++)
        {
            using var failed = await owner.Http.PostAsJsonAsync("/api/v1/account/login", new AccountInput { Email = account.Email, Password = "Wrong-password!123" });
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }
        using var denied = await owner.Http.PostAsJsonAsync("/api/v1/account/login", account);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.False((await owner.GetAsync<SessionModel>("session")).Authenticated);
        using var protectedRead = await owner.Http.GetAsync("/api/v1/settings");
        Assert.Equal(HttpStatusCode.Unauthorized, protectedRead.StatusCode);
    }
}
