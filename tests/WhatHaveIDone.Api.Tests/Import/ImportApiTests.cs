using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Import;
using WhatHaveIDone.API.Models;
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
        Assert.Equal(ImportCallStatus.Todo, Assert.Single((await StoredAsync(owner,id)).Calls).Status);
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
    [Fact] public async Task QuotaWaitAndDurablePageCheckpointSurviveAWorkerReplacementAsync()
    {
        using var owner=await fixture.CreateOwnerAsync();var secret="quota"+Guid.NewGuid().ToString("N");var reportStart = fixture.Toggl.ReportBodies.Count;var id=await owner.PostAsync<string>("imports/prepare",new PrepareApiImport(secret));
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
        Assert.Collection(checkpoint.Calls, call => Assert.Equal(ImportCallStatus.Done, call.Status), call =>
        {
            Assert.Equal(2,call.FirstRow);
            Assert.Equal(ImportCallStatus.Todo, call.Status);
        });
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
        Assert.All(fixture.Toggl.ReportBodies.Skip(reportStart),body=>{using var json=System.Text.Json.JsonDocument.Parse(body);Assert.Equal(42,json.RootElement.GetProperty("user_ids")[0].GetInt32());Assert.Equal(0,json.RootElement.GetProperty("rounding").GetInt32());Assert.Equal("1970-01-01",json.RootElement.GetProperty("start_date").GetString());Assert.Equal("DESC",json.RootElement.GetProperty("order_dir").GetString());});
    }
    [Fact]
    public async Task WorkerProcessesDifferentUsersInParallelWithoutClaimingEitherPageTwiceAsync()
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
        var competing = Task.CompletedTask;
        try
        {
            await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            competing = fixture.StepImportAsync();
            await competing.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(2, entered);
            Assert.Equal(new DateOnly(2020, 1, 1), (await StoredAsync(first, firstId)).RequestedFrom);
        }
        finally
        {
            release.TrySetResult();
            try { await Task.WhenAll(running, competing); }
            finally { fixture.Toggl.BeforeReport = null; }
        }

        Assert.Equal(1, (await ModelAsync(first, firstId)).Imported);
        Assert.Equal(1, (await ModelAsync(second, secondId)).Imported);
        await first.PostAsync<bool>($"imports/{firstId}/cancel", null);
        await second.PostAsync<bool>($"imports/{secondId}/cancel", null);
    }

    [Fact] public async Task AFeaturePaymentResponseRequiresAttentionAndNewTokensCannotSwitchTheSourceIdentityAsync()
    {
        using var owner=await fixture.CreateOwnerAsync();var id=await owner.PostAsync<string>("imports/prepare",new PrepareApiImport("premium"+Guid.NewGuid().ToString("N")));
        await UntilAsync(owner,id,ImportStatus.AwaitingConfirmation);await owner.PostAsync<bool>("imports/"+id+"/confirm",new ConfirmImportInput([7]));await UntilAsync(owner,id,ImportStatus.NeedsAttention);
        var failed=await ModelAsync(owner,id);Assert.Equal("ImportPlan",failed.MessageCode);Assert.Null(failed.NextAttemptAt);
        Assert.Equal(ImportCallStatus.Error, Assert.Single((await StoredAsync(owner,id)).Calls).Status);
        using var wrongSource = await owner.Http.PostAsJsonAsync("/api/v1/imports/"+id+"/resume", new PrepareApiImport("other"+Guid.NewGuid().ToString("N")));
        Assert.Equal(HttpStatusCode.BadRequest, wrongSource.StatusCode);
        Assert.Equal("ImportToken", (await wrongSource.Content.ReadFromJsonAsync<ApiError>())!.Code);
        Assert.Equal(42,(await StoredAsync(owner,id)).SourceUserId);
        await owner.PostAsync<bool>("imports/"+id+"/cancel",null);Assert.Null((await StoredAsync(owner,id)).ProtectedToken);
    }
}
