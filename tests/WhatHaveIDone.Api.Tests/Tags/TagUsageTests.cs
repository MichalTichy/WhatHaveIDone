using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WhatHaveIDone.API.Models;

namespace WhatHaveIDone.Api.Tests;

[Collection("postgres")]
public sealed class TagUsageTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task RecentTagSuggestionsFollowProjectHistoryAndOwnerAndDoNotUseTheGlobalCatalogAsync()
    {
        using var owner = await fixture.CreateOwnerAsync();
        using var other = await fixture.CreateOwnerAsync();
        var alpha = await owner.PostAsync<string>("projects", new ProjectModel { Name = "Alpha" });
        var beta = await owner.PostAsync<string>("projects", new ProjectModel { Name = "Beta" });
        var empty = await owner.PostAsync<string>("projects", new ProjectModel { Name = "Empty" });
        var now = fixture.Clock.GetUtcNow();
        EditTimeEntry Input(string project, int hours, params string[] tags) => new()
        {
            CreateRequestId = Guid.NewGuid(), ProjectId = project, Start = now.AddHours(hours),
            End = now.AddHours(hours).AddMinutes(10), Tags = [.. tags]
        };
        await owner.PostAsync<TimeEntryModel>("time-entries", Input(alpha, -4, "Older", " Shared "));
        var latest = await owner.PostAsync<TimeEntryModel>("time-entries", Input(alpha, -2, " Recent ", "SHARED", "Second"));
        await owner.PostAsync<TimeEntryModel>("time-entries", Input(alpha, -1));
        await owner.PostAsync<TimeEntryModel>("time-entries", Input(beta, -1, "Beta only"));
        await owner.PostAsync<TimeEntryModel>("time-entries", new EditTimeEntry { CreateRequestId = Guid.NewGuid(), Start = now, End = now, Tags = ["No project"] });

        Assert.Equal(["Recent", "Shared", "Second", "Older"], await owner.GetAsync<List<string>>("projects/" + alpha + "/recent-tags"));
        Assert.Equal(["Beta only"], await owner.GetAsync<List<string>>("projects/" + beta + "/recent-tags"));
        Assert.Empty(await owner.GetAsync<List<string>>("projects/" + empty + "/recent-tags"));
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.GetAsync("/api/v1/projects/" + alpha + "/recent-tags")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.Http.GetAsync("/api/v1/projects/missing/recent-tags")).StatusCode);
        var project = (await owner.GetAsync<List<ProjectModel>>("projects")).Single(p => p.Id == alpha);
        await owner.PutAsync<string>("projects/" + alpha, project with { Archived = true });
        Assert.Equal(4, (await owner.GetAsync<List<string>>("projects/" + alpha + "/recent-tags")).Count);
        await owner.PutAsync<TimeEntryModel>("time-entries/" + Uri.EscapeDataString(latest.Id), Input(alpha, -2) with { Version = latest.Version });
        Assert.Equal(["Older", "Shared"], await owner.GetAsync<List<string>>("projects/" + alpha + "/recent-tags"));
    }

    [Fact]
    public async Task UsageTrimsAndDeduplicatesTagsAndAllSearchesIgnoreCaseAsync()
    {
        using var owner = await fixture.CreateOwnerAsync();
        using var other = await fixture.CreateOwnerAsync();
        using var explicitCreation = await owner.Http.PostAsJsonAsync("/api/v1/tags", new { name = "Unused" });
        Assert.Equal(HttpStatusCode.NotFound, explicitCreation.StatusCode);
        Assert.Empty(await owner.GetAsync<List<string>>("tags"));

        var input = new EditTimeEntry
        {
            CreateRequestId = Guid.NewGuid(), Description = "Tagged work",
            Start = fixture.Clock.GetUtcNow().AddHours(-2), End = fixture.Clock.GetUtcNow().AddHours(-1),
            Tags = ["  Vývoj  ", "VÝVOJ", "\tQA\t", "  ", "", " Cafe\u0301 "]
        };
        var entry = await owner.PostAsync<TimeEntryModel>("time-entries", input);
        Assert.Equal(["Vývoj", "QA", "Café"], entry.Tags);
        await other.PostAsync<TimeEntryModel>("time-entries", input with { CreateRequestId = Guid.NewGuid(), Tags = ["Vývoj", "Foreign only"] });

        var tags = await owner.GetAsync<List<string>>("tags");
        Assert.Equal(3, tags.Count);
        var search = Uri.EscapeDataString("  vÝ  ");
        var found = Assert.Single(await owner.GetAsync<List<string>>("tags?search=" + search));
        Assert.Equal("Vývoj", found);
        Assert.Empty(await owner.GetAsync<List<string>>("tags?search=FOREIGN"));
        Assert.Equal("Café", Assert.Single(await owner.GetAsync<List<string>>("tags?search=" + Uri.EscapeDataString(" cAfE\u0301 "))));
        var compatible = await owner.Http.GetFromJsonAsync<List<JsonElement>>("/api/v9/workspaces/ignored/tags?search=" + search);
        var compatibleTag = Assert.Single(compatible!);
        var tagId = compatibleTag.GetProperty("id").GetInt64();
        Assert.True(tagId > 0);
        Assert.Equal(found, compatibleTag.GetProperty("name").GetString());
        Assert.Equal(1, compatibleTag.GetProperty("wid").GetInt32());
        Assert.Equal(1, compatibleTag.GetProperty("workspace_id").GetInt32());
        Assert.Empty((await other.Http.GetFromJsonAsync<List<JsonElement>>("/api/v9/workspaces/ignored/tags?search=QA"))!);

        var query = "from=2026-10-26T00:00:00Z&to=2026-10-27T00:00:00Z&tag=" + Uri.EscapeDataString("  vÝvOj  ");
        Assert.Equal(entry.Id, Assert.Single(await owner.GetAsync<List<TimeEntryModel>>("time-entries?" + query)).Id);
        Assert.Empty(await owner.GetAsync<List<TimeEntryModel>>("time-entries?from=2026-10-26T00:00:00Z&to=2026-10-27T00:00:00Z&tag=FOREIGN"));
        var report = await owner.GetAsync<ReportModel>("reports?" + query);
        Assert.Single(report.Entries); Assert.Equal(3600, report.TotalSeconds);
        Assert.Contains("Tagged work", await owner.Http.GetStringAsync("/api/v1/reports.csv?" + query));

        var repeated = await owner.PostAsync<TimeEntryModel>("time-entries", input with { CreateRequestId = Guid.NewGuid(), Tags = [" výVOJ ", "qa"] });
        Assert.Equal(["Vývoj", "QA"], repeated.Tags);
        Assert.Equal(tags, await owner.GetAsync<List<string>>("tags"));
        var repeatedCompatible = await owner.Http.GetFromJsonAsync<List<JsonElement>>("/api/v9/workspaces/999/tags?search=" + search);
        Assert.Equal(tagId, Assert.Single(repeatedCompatible!).GetProperty("id").GetInt64());
        var running = await owner.PostAsync<TimeEntryModel>("timer/start", new EditTimeEntry { Tags = [" VÝVOJ ", "  Timer tag  "] });
        Assert.Equal(["Vývoj", "Timer tag"], running.Tags);
        Assert.Equal(4, (await owner.GetAsync<List<string>>("tags")).Count);
    }
}
