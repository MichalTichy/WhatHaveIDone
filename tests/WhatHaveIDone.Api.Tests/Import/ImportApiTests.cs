using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Import;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Infrastructure.Persistence;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Tenancy;
using WhatHaveIDone.Logic.Import;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Api.Tests;

[Collection("postgres")]
public sealed class ImportApiTests(IntegrationFixture fixture)
{
    [Fact] public async Task Http429WaitHonorsRetryAfterAndResumesWithoutLosingTheJobAsync()
    {
        using var owner=await fixture.CreateOwnerAsync();var before=fixture.Clock.GetUtcNow();
        var id=await owner.PostAsync<string>("imports/prepare",new PrepareApiImport("throttled"+Guid.NewGuid().ToString("N")));
        await owner.PostAsync<bool>("imports/"+id+"/confirm",new ConfirmImportInput([7]));
        await fixture.StepImportAsync();var waiting=await ModelAsync(owner,id);
        Assert.Equal(ImportStatus.Waiting,waiting.Status);Assert.Equal("ImportQuota",waiting.MessageCode);Assert.Equal(before.AddMinutes(2),waiting.NextAttemptAt);
        var responseJson = await owner.Http.GetStringAsync("/api/v1/imports");
        using var document = System.Text.Json.JsonDocument.Parse(responseJson);
        var wireRun = document.RootElement.EnumerateArray().Single(r => r.GetProperty("id").GetString() == id);
        Assert.Equal("Waiting", wireRun.GetProperty("status").GetString());
        Assert.Null(Assert.Single((await StoredAsync(owner,id)).PendingCalls).FirstRow);
        await fixture.StepImportAsync();Assert.Equal(ImportStatus.Waiting,(await ModelAsync(owner,id)).Status);
        Assert.Empty(await owner.GetAsync<List<TimeEntryModel>>("time-entries?"+Range));
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));await fixture.StepImportAsync();
        Assert.Equal(1002, Assert.Single(await owner.GetAsync<List<TimeEntryModel>>("time-entries?"+Range)).CompatibilityId);
        await owner.PostAsync<bool>("imports/"+id+"/cancel",null);
    }
    private static string Range=>"from=2019-01-01T00%3A00%3A00Z&to=2030-01-01T00%3A00%3A00Z";
    private async Task<ImportRun> StoredAsync(OwnerClient owner,string id)
    {await using var scope=fixture.App.Services.CreateAsyncScope();scope.ServiceProvider.GetRequiredService<CurrentTenancyProvider>().SetBackgroundUser(owner.Owner);return await scope.ServiceProvider.GetRequiredService<IReadOnlyRepository<ImportRun>>().RequireAsync(id,CancellationToken.None);}
    private async Task<ImportModel> ModelAsync(OwnerClient owner,string id)=>(await owner.GetAsync<List<ImportModel>>("imports")).Single(r=>r.Id==id);
    private async Task UntilAsync(OwnerClient owner,string id,ImportStatus status)
    {for(var i=0;i<15;i++){if((await ModelAsync(owner,id)).Status==status)return;await fixture.StepImportAsync();}Assert.Equal(status,(await ModelAsync(owner,id)).Status);}

    [Fact]
    public async Task MultiYearHistoryUsesContiguousNewestFirstWindowsAsync()
    {
        using var owner = await fixture.CreateOwnerAsync();
        var from = new DateOnly(2019, 1, 1);
        var through = new DateOnly(2022, 1, 1);
        var before = fixture.Toggl.ReportBodies.Count;
        var id = await owner.PostAsync<string>("imports/prepare", new PrepareApiImport("windows-" + Guid.NewGuid(), from, through));
        await owner.PostAsync<bool>($"imports/{id}/confirm", new ConfirmImportInput([7]));
        await UntilAsync(owner, id, ImportStatus.Completed);

        var expectedThrough = through;
        foreach (var body in fixture.Toggl.ReportBodies.Skip(before))
        {
            using var json = System.Text.Json.JsonDocument.Parse(body);
            var start = DateOnly.ParseExact(json.RootElement.GetProperty("start_date").GetString()!, "yyyy-MM-dd");
            var end = DateOnly.ParseExact(json.RootElement.GetProperty("end_date").GetString()!, "yyyy-MM-dd");
            Assert.Equal(expectedThrough, end);
            Assert.InRange(end.DayNumber - start.DayNumber, 0, 364);
            expectedThrough = start.AddDays(-1);
        }
        Assert.Equal(from.AddDays(-1), expectedThrough);
    }

    [Fact]
    public async Task LegacyQueueUpgradeKeepsThePendingPageAndDetectsCursorLoopsAsync()
    {
        using var owner = await fixture.CreateOwnerAsync();
        var token = "loop-" + Guid.NewGuid().ToString("N");
        var id = await owner.PostAsync<string>("imports/prepare", new PrepareApiImport(token));
        var database = fixture.App.Services.GetRequiredService<NpgsqlDataSource>();
        await using var seed = database.CreateCommand("""
            UPDATE tracking.mt_doc_importrun
            SET data = (data - 'PendingCalls' - 'ConsumedCursors') || '{
                "Status": "Running",
                "Calls": [
                    { "WorkspaceId": 7, "From": "1970-01-01", "Through": "2026-11-01", "FirstId": 1001, "FirstRow": 2, "Status": "Done" },
                    { "WorkspaceId": 7, "From": "1970-01-01", "Through": "2026-11-01", "FirstId": 1000, "FirstRow": 3, "Status": "Todo" }
                ],
                "Confirmed": true,
                "LeaseUntil": "2099-01-01T00:00:00Z"
            }'::jsonb
            WHERE id = @id;
            """);
        seed.Parameters.AddWithValue("id", id);
        Assert.Equal(1, await seed.ExecuteNonQueryAsync());
        await ImportQueueMigration.ApplyAsync(database);
        await ImportQueueMigration.ApplyAsync(database);
        var migrated = await StoredAsync(owner, id);
        Assert.Equal(3, Assert.Single(migrated.PendingCalls).FirstRow);
        Assert.Equal("7:1001:2", Assert.Single(migrated.ConsumedCursors));

        await fixture.StepImportAsync();
        var stopped = await StoredAsync(owner, id);
        Assert.Equal(ImportStatus.NeedsAttention, stopped.Status);
        Assert.Equal("ImportSource", stopped.MessageCode);
        Assert.Equal(3, Assert.Single(stopped.PendingCalls).FirstRow);
        await owner.PostAsync<bool>($"imports/{id}/cancel", null);
    }
    [Fact] public async Task QuotaWaitAndDurablePageCheckpointSurviveAWorkerReplacementAsync()
    {
        using var owner=await fixture.CreateOwnerAsync();var secret="quota"+Guid.NewGuid().ToString("N");var reportStart = fixture.Toggl.ReportBodies.Count;var id=await owner.PostAsync<string>("imports/prepare",new PrepareApiImport(secret, new DateOnly(2026, 1, 1)));
        await owner.PostAsync<bool>("imports/"+id+"/confirm",new ConfirmImportInput([7]));
        await fixture.StepImportAsync();var waiting=await ModelAsync(owner,id);Assert.Equal(ImportStatus.Waiting,waiting.Status);Assert.Equal("ImportQuota",waiting.MessageCode);Assert.True(waiting.NextAttemptAt>fixture.Clock.GetUtcNow());
        var key=await owner.PostAsync<CreatedApiToken>("tokens",new CreateApiKeyRequest("During import"));using var api=fixture.App.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions{HandleCookies=false});
        api.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes(key.Token+":api_token")));
        (await api.GetAsync("/api/v9/me/time_entries?start_date=2020-01-01T00%3A00%3A00Z&end_date=2030-01-01T00%3A00%3A00Z")).EnsureSuccessStatusCode();
        var stored=await StoredAsync(owner,id);Assert.DoesNotContain(secret,stored.ProtectedToken!);
        var path=fixture.App.Services.GetRequiredService<IConfiguration>()["DataProtection:Path"]!;
        var protection=DataProtectionProvider.Create(new DirectoryInfo(path),o=>o.SetApplicationName("WhatHaveIDone"));
        Assert.Equal(secret,protection.CreateProtector(TogglImportClient.ProtectionPurpose).Unprotect(stored.ProtectedToken!));
        Assert.Empty(await owner.GetAsync<List<TimeEntryModel>>("time-entries?"+Range));
        Assert.Empty(await owner.GetAsync<List<string>>("tags"));
        fixture.Clock.Advance(TimeSpan.FromHours(1)+TimeSpan.FromSeconds(1));
        await fixture.StepImportAsync();
        var checkpoint=await StoredAsync(owner,id);
        Assert.Equal(2, Assert.Single(checkpoint.PendingCalls).FirstRow);
        Assert.Equal(1,checkpoint.Imported);
        Assert.Equal(1,checkpoint.Skipped);
        Assert.Equal(1002, Assert.Single(await owner.GetAsync<List<TimeEntryModel>>("time-entries?"+Range)).CompatibilityId);
        var replacement=new ImportWorker(fixture.App.Services.GetRequiredService<IServiceScopeFactory>(),fixture.Clock,NullLogger<ImportWorker>.Instance);
        await replacement.ProcessNextAsync(CancellationToken.None);
        var completed=await StoredAsync(owner,id);Assert.Equal(ImportStatus.Completed,completed.Status);Assert.Null(completed.ProtectedToken);Assert.Equal(2,completed.Imported);
        Assert.Equal("Imported",Assert.Single(await owner.GetAsync<List<string>>("tags")));
        var importedTags=await owner.Http.GetFromJsonAsync<List<System.Text.Json.JsonElement>>("/api/v9/workspaces/ignored/tags");
        var importedTag=Assert.Single(importedTags!);Assert.Equal(11,importedTag.GetProperty("id").GetInt64());Assert.Equal("Imported",importedTag.GetProperty("name").GetString());
        var defaultClient=Assert.Single(await owner.GetAsync<List<ClientModel>>("clients"));
        Assert.All(await owner.GetAsync<List<ProjectModel>>("projects"),p=>Assert.Equal(defaultClient.Id,p.ClientId));
        var entries=await owner.GetAsync<List<TimeEntryModel>>("time-entries?"+Range);Assert.Equal(new long[]{1001,1002},entries.Select(e=>e.CompatibilityId).Order().ToArray());Assert.All(entries,e=>Assert.Single(e.Tags));
        Assert.All(fixture.Toggl.ReportBodies.Skip(reportStart),body=>{using var json=System.Text.Json.JsonDocument.Parse(body);Assert.Equal(42,json.RootElement.GetProperty("user_ids")[0].GetInt32());Assert.Equal(0,json.RootElement.GetProperty("rounding").GetInt32());Assert.Equal("2026-01-01",json.RootElement.GetProperty("start_date").GetString());Assert.Equal("DESC",json.RootElement.GetProperty("order_dir").GetString());});
    }
    [Fact]
    public async Task WorkerProcessesDifferentUsersInParallelAndDiscardsCancelledPagesAsync()
    {
        using var first = await fixture.CreateOwnerAsync();
        using var second = await fixture.CreateOwnerAsync();
        var tokens = new[] { "parallel-a-" + Guid.NewGuid().ToString("N"), "parallel-b-" + Guid.NewGuid().ToString("N") };
        var firstId = await first.PostAsync<string>("imports/prepare", new PrepareApiImport(tokens[0], new DateOnly(2020, 1, 1)));
        var secondId = await second.PostAsync<string>("imports/prepare", new PrepareApiImport(tokens[1]));
        await first.PostAsync<bool>($"imports/{firstId}/confirm", new ConfirmImportInput([7]));
        await second.PostAsync<bool>($"imports/{secondId}/confirm", new ConfirmImportInput([7]));
        var bothEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;
        fixture.Toggl.BeforeReport = async (token, ct) =>
        {
            if (!tokens.Contains(token)) return;
            if (Interlocked.Increment(ref entered) == 2) bothEntered.TrySetResult();
            await release.Task.WaitAsync(ct);
        };

        var running = fixture.StepImportAsync();
        try
        {
            await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(new DateOnly(2020, 1, 1), (await StoredAsync(first, firstId)).RequestedFrom);
            await first.PostAsync<bool>($"imports/{firstId}/cancel", null);
        }
        finally
        {
            release.TrySetResult();
            try { await running; }
            finally { fixture.Toggl.BeforeReport = null; }
        }

        var cancelled = await StoredAsync(first, firstId);
        Assert.Equal(ImportStatus.Cancelled, cancelled.Status);
        Assert.Equal(0, cancelled.Imported);
        Assert.Empty(await first.GetAsync<List<TimeEntryModel>>("time-entries?" + Range));
        Assert.Empty(cancelled.PendingCalls);
        Assert.Equal(1, (await ModelAsync(second, secondId)).Imported);
        await second.PostAsync<bool>($"imports/{secondId}/cancel", null);
    }

    [Fact] public async Task AFeaturePaymentResponseRequiresAttentionAndNewTokensCannotSwitchTheSourceIdentityAsync()
    {
        using var owner=await fixture.CreateOwnerAsync();var id=await owner.PostAsync<string>("imports/prepare",new PrepareApiImport("premium"+Guid.NewGuid().ToString("N")));
        await UntilAsync(owner,id,ImportStatus.AwaitingConfirmation);await owner.PostAsync<bool>("imports/"+id+"/confirm",new ConfirmImportInput([7]));await UntilAsync(owner,id,ImportStatus.NeedsAttention);
        var failed=await ModelAsync(owner,id);Assert.Equal("ImportPlan",failed.MessageCode);Assert.Null(failed.NextAttemptAt);
        Assert.Null(Assert.Single((await StoredAsync(owner,id)).PendingCalls).FirstRow);
        using var wrongSource = await owner.Http.PostAsJsonAsync("/api/v1/imports/"+id+"/resume", new PrepareApiImport("other"+Guid.NewGuid().ToString("N")));
        Assert.Equal(HttpStatusCode.BadRequest, wrongSource.StatusCode);
        Assert.Equal("ImportToken", (await wrongSource.Content.ReadFromJsonAsync<ApiError>())!.Code);
        Assert.Equal(42,(await StoredAsync(owner,id)).SourceUserId);
        await owner.PostAsync<bool>("imports/"+id+"/cancel",null);Assert.Null((await StoredAsync(owner,id)).ProtectedToken);
    }
}
