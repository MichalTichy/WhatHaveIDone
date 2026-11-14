using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Infrastructure.Identity;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Api.Tests;

[Collection("postgres")]
public sealed class TrackingApiTests(IntegrationFixture fixture)
{
    [Fact] public async Task StoredInstantsPreserveBothDstOffsetsAndFractionalSecondsAsync()
    {
        using var owner=await fixture.CreateOwnerAsync();
        var input=Input("Repeated local hour") with
        {
            Start=DateTimeOffset.Parse("2026-10-25T02:15:00.1234567+02:00"),
            End=DateTimeOffset.Parse("2026-10-25T02:15:00.7654321+01:00")
        };
        var created=await owner.PostAsync<TimeEntryModel>("time-entries",input);
        var loaded=await owner.GetAsync<TimeEntryModel>("time-entries/"+Uri.EscapeDataString(created.Id));
        Assert.Equal(input.Start.UtcTicks,created.Start.UtcTicks);Assert.Equal(input.End.Value.UtcTicks,created.End!.Value.UtcTicks);
        Assert.Equal(input.Start.UtcTicks,loaded.Start.UtcTicks);Assert.Equal(input.End.Value.UtcTicks,loaded.End!.Value.UtcTicks);
    }
    private EditTimeEntry Input(string description="",string? project=null)=>new(){CreateRequestId=Guid.NewGuid(),Description=description,ProjectId=project,Start=fixture.Clock.GetUtcNow().AddHours(-2),End=fixture.Clock.GetUtcNow().AddHours(-1)};
    private static string Range=>"from=2019-01-01T00%3A00%3A00Z&to=2030-01-01T00%3A00%3A00Z";
    [Fact] public async Task AnonymousMutationsRequireAntiforgeryAndProtectedReadsDoNotRedirectAsync()
    {
        using var client=fixture.App.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/v1/account/register",new AccountInput{Email="csrf@example.test",Password="Unsent!123456"})).StatusCode);
        var read=await client.GetAsync("/api/v1/projects");Assert.Equal(HttpStatusCode.Unauthorized,read.StatusCode);Assert.Null(read.Headers.Location);
    }
    [Fact] public async Task OwnersCannotReadOrReferenceEachOthersRecordsAsync()
    {
        using var alice=await fixture.CreateOwnerAsync();using var bob=await fixture.CreateOwnerAsync();
        var project=await alice.PostAsync<string>("projects",new ProjectModel{Name="Private",Color="#009cde"});
        var entry=await alice.PostAsync<TimeEntryModel>("time-entries",Input("Private work",project));
        Assert.Equal(HttpStatusCode.NotFound,(await bob.Http.GetAsync("/api/v1/time-entries/"+Uri.EscapeDataString(entry.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await bob.Http.PostAsJsonAsync("/api/v1/time-entries",Input("",project))).StatusCode);
        Assert.Empty(await bob.GetAsync<List<TimeEntryModel>>("time-entries?"+Range));
        var settings=await alice.GetAsync<SettingsModel>("settings");Assert.False(settings.OnboardingCompleted);
        await using var scope=fixture.App.Services.CreateAsyncScope();var user=await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByIdAsync(alice.Owner);Assert.False(user!.EmailConfirmed);
    }
    [Fact] public async Task ConcurrentStartsLeaveOneTimerAndStoppingTwiceDoesNotDuplicateAsync()
    {
        using var owner=await fixture.CreateOwnerAsync();
        var responses=await Task.WhenAll(owner.Http.PostAsJsonAsync("/api/v1/timer/start",new EditTimeEntry()),owner.Http.PostAsJsonAsync("/api/v1/timer/start",new EditTimeEntry{Description="Other"}));
        Assert.All(responses,r=>Assert.Contains(r.StatusCode,new[]{HttpStatusCode.OK,HttpStatusCode.Conflict}));
        var running=await owner.GetAsync<TimeEntryModel>("timer");var entries=await owner.GetAsync<List<TimeEntryModel>>("time-entries?"+Range);
        Assert.Single(entries,e=>e.End is null);Assert.Equal(running.Id,entries.Single(e=>e.End is null).Id);
        Assert.True(await owner.PostAsync<bool>("timer/"+Uri.EscapeDataString(running.Id)+"/stop",null));Assert.False(await owner.PostAsync<bool>("timer/"+Uri.EscapeDataString(running.Id)+"/stop",null));
        Assert.Equal(entries.Count,(await owner.GetAsync<List<TimeEntryModel>>("time-entries?"+Range)).Count);
        foreach(var response in responses)response.Dispose();
    }
    [Fact] public async Task StaleEditsFailAndRetryingCreationUsesOneIdentityAsync()
    {
        using var owner=await fixture.CreateOwnerAsync();var input=Input();
        var first=await owner.PostAsync<TimeEntryModel>("time-entries",input);var repeated=await owner.PostAsync<TimeEntryModel>("time-entries",input);Assert.Equal(first.Id,repeated.Id);
        var edited=await owner.PutAsync<TimeEntryModel>("time-entries/"+Uri.EscapeDataString(first.Id),input with{Version=first.Version,Description="Saved"});
        Assert.NotEqual(first.Version,edited.Version);
        Assert.Equal(HttpStatusCode.Conflict,(await owner.Http.PutAsJsonAsync("/api/v1/time-entries/"+Uri.EscapeDataString(first.Id),input with{Version=first.Version,Description="Stale"})).StatusCode);
        Assert.Equal("Saved",(await owner.GetAsync<TimeEntryModel>("time-entries/"+Uri.EscapeDataString(first.Id))).Description);
    }
    [Fact] public async Task DynamicTagsDeduplicateAndArchivedProjectsRemainOnExistingRecordsAsync()
    {
        using var owner=await fixture.CreateOwnerAsync();var projectId=await owner.PostAsync<string>("projects",new ProjectModel{Name="Archive me"});
        var input=Input("",projectId)with{Tags=[" vývoj ","VÝVOJ"]};var entry=await owner.PostAsync<TimeEntryModel>("time-entries",input);
        Assert.Single(await owner.GetAsync<List<string>>("tags"));Assert.Single(entry.Tags);
        var project=(await owner.GetAsync<List<ProjectModel>>("projects")).Single();await owner.PutAsync<string>("projects/"+projectId,project with{Archived=true});
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.Http.PostAsJsonAsync("/api/v1/timer/start",new EditTimeEntry{ProjectId=projectId})).StatusCode);
        var changed=await owner.PutAsync<TimeEntryModel>("time-entries/"+Uri.EscapeDataString(entry.Id),input with{Version=entry.Version,Description="Still belongs here"});Assert.Equal(projectId,changed.ProjectId);
    }
    [Fact] public async Task ReportsClipMidnightAndDstAndCsvUsesTheSameSelectionAsync()
    {
        using var owner=await fixture.CreateOwnerAsync();var input=Input("DST")with{Start=DateTimeOffset.Parse("2026-03-29T00:00:00+01:00"),End=DateTimeOffset.Parse("2026-03-30T00:00:00+02:00"),Tags=["filter"]};
        await owner.PostAsync<TimeEntryModel>("time-entries",input);
        var query="from=2026-03-28T23%3A00%3A00Z&to=2026-03-29T22%3A00%3A00Z&tag=filter&text=dst";
        var report=await owner.GetAsync<ReportModel>("reports?"+query);Assert.Equal(23*3600,report.TotalSeconds);Assert.Single(report.Days);Assert.Equal(report.TotalSeconds,report.Days[0].Seconds);
        var csv=await owner.Http.GetStringAsync("/api/v1/reports.csv?"+query);Assert.Contains("82800",csv);Assert.Contains("DST",csv);
    }
    [Fact] public async Task ZeroLengthEntriesAtThePeriodBoundaryRemainVisibleAsync()
    {
        using var owner=await fixture.CreateOwnerAsync();var midnight=DateTimeOffset.Parse("2026-10-26T00:00Z");await owner.PostAsync<TimeEntryModel>("time-entries",Input()with{Start=midnight,End=midnight});
        Assert.Single(await owner.GetAsync<List<TimeEntryModel>>("time-entries?from=2026-10-26T00%3A00%3A00Z&to=2026-10-27T00%3A00%3A00Z"));
    }
    private sealed class LegacyProject
    {
        [Newtonsoft.Json.JsonProperty("id")] public string? Id { get; set; }
        [Newtonsoft.Json.JsonProperty("wid")] public string? WorkspaceId { get; set; }
        [Newtonsoft.Json.JsonProperty("cid")] public string? ClientId { get; set; }
        [Newtonsoft.Json.JsonProperty("color")] public string? HexColor { get; set; }
    }
    private sealed class LegacyEntry
    {
        [Newtonsoft.Json.JsonProperty("id")] public string? Id { get; set; }
        [Newtonsoft.Json.JsonProperty("wid")] public string? WorkspaceId { get; set; }
        [Newtonsoft.Json.JsonProperty("pid")] public string? ProjectId { get; set; }
        [Newtonsoft.Json.JsonProperty("stop")] public DateTimeOffset? Stop { get; set; }
        [Newtonsoft.Json.JsonProperty("duration")] public long? Duration { get; set; }
        [Newtonsoft.Json.JsonProperty("tags")] public List<string>? Tags { get; set; }
    }
}
