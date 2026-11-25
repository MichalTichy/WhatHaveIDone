using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Web.Resources;

namespace WhatHaveIDone.Web.Features.Feedback;

public partial class FeedbackDialog
{
    [Inject]
    private IJSRuntime Js { get; set; } = null!;

    [Parameter]
    public EventCallback Closed { get; set; }

    private ElementReference dialog;
    private DotNetObjectReference<FeedbackDialog>? self;
    private string text = "";
    private bool showPosts, hasMore;
    private List<FeedbackModel>? posts;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            self = DotNetObjectReference.Create(this);
            await Js.InvokeVoidAsync("whid.openDialog", Cancellation, dialog, self);
        }
    }

    private async Task TogglePostsAsync()
    {
        showPosts = !showPosts;
        Error = null;
        if (showPosts)
        {
            posts = null;
            await LoadMoreAsync();
        }
    }

    private async Task LoadMoreAsync()
    {
        await ExecuteLoadAsync(ct => Api.GetFeedbackAsync(posts?.Count ?? 0, ct), result =>
        {
            posts ??= [];
            posts.AddRange(result);
            hasMore = result.Count == 50;
        });
    }

    private async Task ResolveAsync(FeedbackModel post)
    {
        if (Busy)
        {
            return;
        }
        await RunAsync(async ct =>
        {
            await Api.ResolveFeedbackAsync(post.Id, ct);
            // Resolved posts leave the shared list, so the next page offset stays aligned with the server.
            posts?.Remove(post);
        });
    }

    private async Task SubmitAsync()
    {
        if (Busy)
        {
            return;
        }
        await RunAsync(async ct =>
        {
            await Api.SubmitFeedbackAsync(new SubmitFeedback(text), ct);
            if (!IsDisposed)
            {
                Feedback.Show(Strings.FeedbackSent);
                await CloseAsync();
            }
        });
    }

    [JSInvokable]
    public async Task RequestCloseAsync()
    {
        if (!Busy)
        {
            await CloseAsync();
        }
    }

    private async Task CloseAsync()
    {
        await Js.InvokeVoidAsync("whid.closeDialog", Cancellation, dialog);
        await Closed.InvokeAsync();
    }

    public override void Dispose()
    {
        self?.Dispose();
        base.Dispose();
    }
}
