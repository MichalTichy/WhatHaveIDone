using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Infrastructure.LeaderElection;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Import;
using WhatHaveIDone.Contracts;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Api.Tests;

[Collection("postgres")]
public sealed class ImportApiTests(IntegrationFixture fixture)
{
    [Fact] public async Task Http429WaitHonorsRetryAfterAndResumesWithoutLosingTheJobAsync()
    {
        using var owner=await fixture.CreateOwnerAsync();var before=fixture.Clock.GetUtcNow();
        var id=await owner.PostAsync<string>("imports/prepare",new PrepareApiImport("throttled"+Guid.NewGuid().ToString("N")));
        await fixture.StepImportAsync();var waiting=await ModelAsync(owner,id);
        Assert.Equal("Waiting",waiting.Status);Assert.Equal("ImportQuota",waiting.MessageCode);Assert.Equal(before.AddMinutes(2),waiting.NextAttemptAt);
        await fixture.StepImportAsync();Assert.Equal("Waiting",(await ModelAsync(owner,id)).Status);
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));await UntilAsync(owner,id,"AwaitingConfirmation");
        Assert.Empty(await owner.GetAsync<List<TimeEntryModel>>("time-entries?"+Range));
        await owner.PostAsync<bool>("imports/"+id+"/cancel",null);
    }
    [Fact] public void StagingCleanupPreservesActiveFreshAndUnmanagedFiles()
    {
        using var scope=fixture.App.Services.CreateScope();var storage=scope.ServiceProvider.GetRequiredService<ImportStorage>();
        var active=storage.NewPath();var orphan=storage.NewPath();var fresh=storage.NewPath();var unmanaged=Path.Combine(storage.Root,"keep.txt");
        foreach(var path in new[]{active,orphan,fresh,unmanaged})File.WriteAllText(path,"synthetic");
        var now=DateTimeOffset.UtcNow;foreach(var path in new[]{active,orphan,unmanaged})File.SetLastWriteTimeUtc(path,now.AddDays(-2).UtcDateTime);
        storage.CleanOrphans([active],now,CancellationToken.None);
        Assert.True(File.Exists(active));Assert.False(File.Exists(orphan));Assert.True(File.Exists(fresh));Assert.True(File.Exists(unmanaged));
        foreach(var path in new[]{active,fresh,unmanaged})File.Delete(path);
    }
    private static string Range=>"from=2019-01-01T00%3A00%3A00Z&to=2030-01-01T00%3A00%3A00Z";
    private async Task<ImportRun> StoredAsync(OwnerClient owner,string id)
    {await using var scope=fixture.App.Services.CreateAsyncScope();return await scope.ServiceProvider.GetRequiredService<IReadOnlyRepository<ImportRun>>().RequireAsync(owner.Owner,id,CancellationToken.None);}
    private async Task<ImportModel> ModelAsync(OwnerClient owner,string id)=>(await owner.GetAsync<List<ImportModel>>("imports")).Single(r=>r.Id==id);
    private async Task UntilAsync(OwnerClient owner,string id,string status)
    {for(var i=0;i<15;i++){if((await ModelAsync(owner,id)).Status==status)return;await fixture.StepImportAsync();}Assert.Equal(status,(await ModelAsync(owner,id)).Status);}
    [Fact] public async Task QuotaWaitAndDurablePageCheckpointSurviveAWorkerReplacementAsync()
    {
        using var owner=await fixture.CreateOwnerAsync();var secret="quota"+Guid.NewGuid().ToString("N");var id=await owner.PostAsync<string>("imports/prepare",new PrepareApiImport(secret));
        await fixture.StepImportAsync();var waiting=await ModelAsync(owner,id);Assert.Equal("Waiting",waiting.Status);Assert.Equal("ImportQuota",waiting.MessageCode);Assert.True(waiting.NextAttemptAt>fixture.Clock.GetUtcNow());
        var key=await owner.PostAsync<CreatedApiToken>("tokens",new TokenName("During import"));using var api=fixture.App.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions{HandleCookies=false});
        api.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes(key.Token+":api_token")));
        (await api.GetAsync("/api/v9/me/time_entries?start_date=2020-01-01T00%3A00%3A00Z&end_date=2030-01-01T00%3A00%3A00Z")).EnsureSuccessStatusCode();
        var stored=await StoredAsync(owner,id);Assert.DoesNotContain(secret,stored.ProtectedToken!);
        var path=fixture.App.Services.GetRequiredService<IConfiguration>()["DataProtection:Path"]!;
        var protection=DataProtectionProvider.Create(new DirectoryInfo(path),o=>o.SetApplicationName("WhatHaveIDone"));
        Assert.Equal(secret,protection.CreateProtector(TogglImportClient.ProtectionPurpose).Unprotect(stored.ProtectedToken!));
        fixture.Clock.Advance(TimeSpan.FromHours(1)+TimeSpan.FromSeconds(1));await UntilAsync(owner,id,"AwaitingConfirmation");
        Assert.Empty(await owner.GetAsync<List<TimeEntryModel>>("time-entries?"+Range));
        await owner.PostAsync<bool>("imports/"+id+"/confirm",new ConfirmImportInput([7],""));
        await fixture.StepImportAsync();Assert.Empty(await owner.GetAsync<List<TagModel>>("tags"));
        await fixture.StepImportAsync();await fixture.StepImportAsync();
        var checkpoint=await StoredAsync(owner,id);Assert.Equal(2,checkpoint.NextRow);Assert.Equal(1,checkpoint.Imported);Assert.Equal(1,checkpoint.Skipped);
        var replacement=new ImportWorker(fixture.App.Services.GetRequiredService<IServiceScopeFactory>(),fixture.Clock,fixture.App.Services.GetRequiredService<ILeaderElection>(),NullLogger<ImportWorker>.Instance);
        await replacement.ProcessNextAsync(CancellationToken.None);
        var completed=await StoredAsync(owner,id);Assert.Equal("Completed",completed.Status);Assert.Null(completed.ProtectedToken);Assert.Equal(2,completed.Imported);
        var importedTags=await owner.GetAsync<List<TagModel>>("tags");var importedTag=Assert.Single(importedTags);Assert.Equal(11,importedTag.Id);Assert.Equal("Imported",importedTag.Name);
        var defaultClient=Assert.Single(await owner.GetAsync<List<ClientModel>>("clients"));
        Assert.All(await owner.GetAsync<List<ProjectModel>>("projects"),p=>Assert.Equal(defaultClient.Id,p.ClientId));
        var entries=await owner.GetAsync<List<TimeEntryModel>>("time-entries?"+Range);Assert.Equal(new long[]{1001,1002},entries.Select(e=>e.CompatibilityId).Order().ToArray());Assert.All(entries,e=>Assert.Single(e.Tags));
        Assert.All(fixture.Toggl.ReportBodies,body=>{using var json=System.Text.Json.JsonDocument.Parse(body);Assert.Equal(42,json.RootElement.GetProperty("user_ids")[0].GetInt32());Assert.Equal(0,json.RootElement.GetProperty("rounding").GetInt32());Assert.Equal("1970-01-01",json.RootElement.GetProperty("start_date").GetString());});
    }
    [Fact] public async Task AFeaturePaymentResponseRequiresAttentionAndNewTokensCannotSwitchTheSourceIdentityAsync()
    {
        using var owner=await fixture.CreateOwnerAsync();var id=await owner.PostAsync<string>("imports/prepare",new PrepareApiImport("premium"+Guid.NewGuid().ToString("N")));
        await UntilAsync(owner,id,"AwaitingConfirmation");await owner.PostAsync<bool>("imports/"+id+"/confirm",new ConfirmImportInput([7],""));await UntilAsync(owner,id,"NeedsAttention");
        var failed=await ModelAsync(owner,id);Assert.Equal("ImportPlan",failed.MessageCode);Assert.Null(failed.NextAttemptAt);
        await owner.PostAsync<bool>("imports/"+id+"/resume",new PrepareApiImport("other"+Guid.NewGuid().ToString("N")));await fixture.StepImportAsync();
        Assert.Equal("ImportToken",(await ModelAsync(owner,id)).MessageCode);Assert.Equal(42,(await StoredAsync(owner,id)).SourceUserId);
        await owner.PostAsync<bool>("imports/"+id+"/cancel",null);Assert.Null((await StoredAsync(owner,id)).ProtectedToken);
    }
    [Fact] public async Task CsvBatchesPreserveUtf8CursorsAndReimportNeverOverwritesOrResurrectsLocalChangesAsync()
    {
        using var owner=await fixture.CreateOwnerAsync();var csv=Csv(260);var id=await UploadAsync(owner,csv);var preview=await ModelAsync(owner,id);
        Assert.Equal("AwaitingConfirmation",preview.Status);Assert.False(preview.HasOriginalIds);Assert.Equal(261,preview.TotalRows);Assert.Contains("source@example.test",preview.AvailableEmails);
        var path=(await StoredAsync(owner,id)).FilePath!;
        await owner.PostAsync<bool>("imports/"+id+"/confirm",new ConfirmImportInput([],"source@example.test"));await fixture.StepImportAsync();Assert.Equal(250,(await ModelAsync(owner,id)).Imported);
        await fixture.StepImportAsync();Assert.Equal("Completed",(await ModelAsync(owner,id)).Status);Assert.False(File.Exists(path));
        var entries=await owner.GetAsync<List<TimeEntryModel>>("time-entries?"+Range);Assert.Equal(260,entries.Count);Assert.Contains(entries,e=>e.Description.Contains("řádkový",StringComparison.Ordinal));
        var csvDefaultClient=Assert.Single(await owner.GetAsync<List<ClientModel>>("clients"));
        Assert.All(await owner.GetAsync<List<ProjectModel>>("projects"),p=>Assert.Equal(csvDefaultClient.Id,p.ClientId));
        var entry=entries[0];await owner.PutAsync<TimeEntryModel>("time-entries/"+Uri.EscapeDataString(entry.Id),new EditTimeEntry{Version=entry.Version,Description="Local edit",ProjectId=entry.ProjectId,Tags=entry.Tags,Start=entry.Start,End=entry.End});
        var second=await UploadAsync(owner,csv);await owner.PostAsync<bool>("imports/"+second+"/confirm",new ConfirmImportInput([],"source@example.test"));await UntilAsync(owner,second,"Completed");
        Assert.Equal(0,(await ModelAsync(owner,second)).Imported);Assert.Equal("Local edit",(await owner.GetAsync<TimeEntryModel>("time-entries/"+Uri.EscapeDataString(entry.Id))).Description);
        var updated=await owner.GetAsync<TimeEntryModel>("time-entries/"+Uri.EscapeDataString(entry.Id));(await owner.Http.DeleteAsync("/api/v1/time-entries/"+Uri.EscapeDataString(entry.Id)+"?version="+updated.Version)).EnsureSuccessStatusCode();
        var third=await UploadAsync(owner,csv);await owner.PostAsync<bool>("imports/"+third+"/confirm",new ConfirmImportInput([],"source@example.test"));await UntilAsync(owner,third,"Completed");
        Assert.Equal(259,(await owner.GetAsync<List<TimeEntryModel>>("time-entries?"+Range)).Count);
    }
    private async Task<string> UploadAsync(OwnerClient owner,string text)
    {
        using var form=new MultipartFormDataContent();form.Add(new StringContent(text,Encoding.UTF8,"text/csv"),"file","toggl.csv");form.Add(new StringContent("Europe/Prague"),"timeZone");
        using var response=await owner.Http.PostAsync("/api/v1/imports/file",form);response.EnsureSuccessStatusCode();return(await response.Content.ReadFromJsonAsync<string>())!;
    }
    private static string Csv(int count)
    {
        var csv=new StringBuilder("User,Email,Client,Project,Task,Description,Billable,Start date,Start time,End date,End time,Duration,Tags\n");
        for(var i=0;i<count;i++)csv.Append("Person,source@example.test,,CSV project,,\"").Append(i==0?"Víceřádkový,\nobsah \"\"citace\"\"":"Činnost č. "+i).Append("\",No,2026-10-26,09:00:00,2026-10-26,10:00:00,01:00:00,\"vývoj, schůzka\"\n");
        csv.Append("Other,other@example.test,,Other project,,Other,No,2026-10-26,09:00:00,2026-10-26,10:00:00,01:00:00,\n");return csv.ToString();
    }
}
