using System.Net.Http.Json;
using System.Net;
using System.Security.Claims;
using Marten;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shared.Infrastructure.CurrentTenancyProvider;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Import;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Logic.Tenancy;
using WhatHaveIDone.Logic.TimeEntries;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Api.Tests;

[Collection("postgres")]
public sealed class TenancyTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task PlainQueriesAndIdenticalDocumentIdsStayInsideTheAuthenticatedUsersTenantAsync()
    {
        using var alice = await fixture.CreateOwnerAsync(); using var bob = await fixture.CreateOwnerAsync();
        var id = "shared-id-" + Guid.NewGuid().ToString("N");
        var store = fixture.App.Services.GetRequiredService<IDocumentStore>();
        foreach (var (user, description) in new[] { (alice.Owner, "Alice only"), (bob.Owner, "Bob only") })
        {
            await using var session = store.LightweightSession(user);
            session.Store(new TimeEntry { Id = id, CompatibilityId = 123, Description = description, Start = fixture.Clock.GetUtcNow(), End = fixture.Clock.GetUtcNow() });
            await session.SaveChangesAsync();
        }
        Assert.Equal("Alice only", (await alice.GetAsync<TimeEntryModel>("time-entries/" + id)).Description);
        Assert.Equal("Bob only", (await bob.GetAsync<TimeEntryModel>("time-entries/" + id)).Description);
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var accessor = services.GetRequiredService<IHttpContextAccessor>(); var previous = accessor.HttpContext;
        try
        {
            accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, alice.Owner), new Claim("tenant_id", bob.Owner)], "test")) };
            accessor.HttpContext.Request.Headers["X-Tenant-Id"] = bob.Owner;
            Assert.Same(services.GetRequiredService<ICurrentTenancyProvider>(), services.GetRequiredService<CurrentTenancyProvider>());
            Assert.Equal(alice.Owner, await services.GetRequiredService<ICurrentTenancyProvider>().GetUserTenantAsync());
            var entry = await services.GetRequiredService<IReadOnlyRepository<TimeEntry>>().GetByIdAsync(id);
            Assert.Equal("Alice only", entry!.Description);
        }
        finally { accessor.HttpContext = previous; }
        await using var unscoped = store.QuerySession();
        Assert.Null(await unscoped.LoadAsync<TimeEntry>(id));
    }

    [Fact]
    public async Task GlobalApiKeysKeepOwnerChecksAndAnonymousScopesCannotReadTenantDocumentsAsync()
    {
        using var alice = await fixture.CreateOwnerAsync(); using var bob = await fixture.CreateOwnerAsync();
        var key = await alice.PostAsync<CreatedApiToken>("tokens", new CreateApiKeyRequest("Tenant isolation"));
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Http.DeleteAsync("/api/v1/tokens/" + key.Id)).StatusCode);
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var error = await Assert.ThrowsAsync<AppException>(() => scope.ServiceProvider.GetRequiredService<IReadOnlyRepository<UserProfile>>().GetByIdAsync(alice.Owner));
        Assert.Equal("Unauthenticated", error.Code);
        var denied = await Assert.ThrowsAsync<AppException>(() => scope.ServiceProvider.GetRequiredService<ISender>().Send(new StartTimerUsecase(new EditTimeEntry())));
        Assert.Equal("Unauthenticated", denied.Code);
    }

    [Fact]
    public async Task TenantScopedUsecasesNeedNoHttpUserAndKeepRetriesAndProfilesIsolatedAsync()
    {
        using var alice = await fixture.CreateOwnerAsync(); using var bob = await fixture.CreateOwnerAsync();
        var request = new EditTimeEntry { CreateRequestId = Guid.NewGuid(), Description = "Alice only", Tags = [" Review ", "review"],
            Start = fixture.Clock.GetUtcNow().AddHours(-2), End = fixture.Clock.GetUtcNow().AddHours(-1) };
        string id;
        await using (var background = fixture.App.Services.CreateAsyncScope())
        {
            var services = background.ServiceProvider;
            Assert.Null(services.GetRequiredService<IHttpContextAccessor>().HttpContext);
            services.GetRequiredService<CurrentTenancyProvider>().SetBackgroundUser(alice.Owner);
            var sender = services.GetRequiredService<ISender>();
            id = await sender.Send(new SaveTimeEntryUsecase(null, request));
            Assert.Equal(id, await sender.Send(new SaveTimeEntryUsecase(null, request)));
            var profile = (await services.GetRequiredService<IReadOnlyRepository<UserProfile>>().GetBySpecAsync(new UserProfileSpecification()))!;
            await sender.Send(new SaveSettingsUsecase(new SettingsModel { Version = profile.SettingsVersion, Language = "en", TimeZone = "UTC", FirstDayOfWeek = 0 }));
            var running = await sender.Send(new StartTimerUsecase(new EditTimeEntry { Description = "Background timer" }));
            Assert.True(await sender.Send(new StopTimerUsecase(running)));
        }
        Assert.Equal("Alice only", (await alice.GetAsync<TimeEntryModel>("time-entries/" + id)).Description);
        Assert.Equal("en", (await alice.GetAsync<SettingsModel>("settings")).Language);
        Assert.Equal("cs", (await bob.GetAsync<SettingsModel>("settings")).Language);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Http.GetAsync("/api/v1/time-entries/" + id)).StatusCode);
        var other = await bob.PostAsync<TimeEntryModel>("time-entries", request with { Description = "Bob only" });
        Assert.NotEqual(id, other.Id);
        Assert.Equal("Bob only", (await bob.GetAsync<TimeEntryModel>("time-entries/" + other.Id)).Description);
    }

    [Fact]
    public async Task ImportProgressDoesNotInvalidateSettingsOrOverwriteEditsAndCancelledBatchesCannotReturnAsync()
    {
        using var owner = await fixture.CreateOwnerAsync();
        var id = await owner.PostAsync<string>("imports/prepare", new PrepareApiImport("synthetic-" + Guid.NewGuid().ToString("N")));
        var settings = await owner.GetAsync<SettingsModel>("settings");
        await fixture.StepImportAsync();
        Assert.Equal(settings.Version, (await owner.GetAsync<SettingsModel>("settings")).Version);
        var store = fixture.App.Services.GetRequiredService<IDocumentStore>();
        ImportRun stale;
        await using (var read = store.QuerySession(owner.Owner)) stale = (await read.LoadAsync<UserProfile>(owner.Owner))!.Import!;
        await owner.PutAsync<bool>("settings", settings with { Language = "en" });
        var changed = await owner.GetAsync<SettingsModel>("settings");
        Assert.NotEqual(settings.Version, changed.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.Http.PutAsJsonAsync("/api/v1/settings", settings)).StatusCode);

        await using (var background = fixture.App.Services.CreateAsyncScope())
        {
            background.ServiceProvider.GetRequiredService<CurrentTenancyProvider>().SetBackgroundUser(owner.Owner);
            await background.ServiceProvider.GetRequiredService<ISender>().Send(new ApplyImportBatchUsecase(stale, [], []));
        }
        await owner.PostAsync<bool>("imports/" + id + "/cancel", new { });
        await using var scope = fixture.App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentTenancyProvider>().SetBackgroundUser(owner.Owner);
        var conflict = await Assert.ThrowsAsync<AppException>(() => scope.ServiceProvider.GetRequiredService<ISender>().Send(new ApplyImportBatchUsecase(stale, [], [])));
        Assert.Equal("Conflict", conflict.Code);
        await using var verify = store.QuerySession(owner.Owner);
        var profile = (await verify.LoadAsync<UserProfile>(owner.Owner))!;
        Assert.Equal("Cancelled", profile.Import!.Status); Assert.Null(profile.Import.ProtectedToken);
        Assert.Equal("en", profile.Language); Assert.Equal(changed.Version, profile.SettingsVersion);
    }

    [Fact]
    public async Task ImportHistoryIsBoundedAndContainsOnlyFinishedRunsWithoutCredentialsAsync()
    {
        using var owner = await fixture.CreateOwnerAsync();
        for (var i = 0; i < 22; i++)
        {
            var id = await owner.PostAsync<string>("imports/prepare", new PrepareApiImport("synthetic-token-" + i));
            await owner.PostAsync<bool>("imports/" + id + "/cancel", new { });
        }
        var visible = await owner.GetAsync<List<ImportModel>>("imports");
        Assert.Equal(20, visible.Count);
        var store = fixture.App.Services.GetRequiredService<IDocumentStore>();
        await using var session = store.QuerySession(owner.Owner);
        var profile = (await session.LoadAsync<UserProfile>(owner.Owner))!;
        Assert.Equal(19, profile.ImportHistory.Count);
        Assert.All(profile.ImportHistory.Append(profile.Import!), run =>
        {
            Assert.Equal("Cancelled", run.Status); Assert.Null(run.ProtectedToken); Assert.Null(run.FilePath);
            Assert.Empty(run.Preview); Assert.Empty(run.TagNames); Assert.Empty(run.Workspaces);
        });
    }
}
