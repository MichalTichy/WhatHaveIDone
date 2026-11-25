using System.Net;
using System.Net.Http.Json;
using WhatHaveIDone.API.Models;

namespace WhatHaveIDone.Api.Tests;

[Collection("postgres")]
public sealed class FeedbackApiTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task FeedbackIsSharedAndPreservesTextAndServerTimeAsync()
    {
        using var alice = await fixture.CreateOwnerAsync();
        using var bob = await fixture.CreateOwnerAsync();
        var text = "First line\n<script>alert('text only')</script> " + Guid.NewGuid();
        var id = await alice.PostAsync<string>("feedback", new SubmitFeedback("  " + text + "  "));
        var posts = await bob.GetAsync<List<FeedbackModel>>("feedback");
        var post = Assert.Single(posts, p => p.Id == id);
        Assert.Equal(text, post.Text);
        Assert.Equal(fixture.Clock.GetUtcNow(), post.CreatedAt);
        Assert.Contains(await alice.GetAsync<List<FeedbackModel>>("feedback"), p => p.Id == id);
    }

    [Fact]
    public async Task FeedbackRequiresAuthenticationAndRejectsBlankOrOversizedTextAsync()
    {
        using var anonymous = fixture.App.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/feedback")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/feedback", new SubmitFeedback("test"))).StatusCode);
        using var owner = await fixture.CreateOwnerAsync();
        foreach (var text in new[] { " \n\t", new string('x', 5001), null })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.Http.PostAsJsonAsync("/api/v1/feedback", new { Text = text })).StatusCode);
        }
    }
}
