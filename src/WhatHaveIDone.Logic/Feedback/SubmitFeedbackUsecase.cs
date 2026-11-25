using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Feedback;

public sealed record SubmitFeedbackUsecase(string Text) : IRequest<string>;

public sealed class SubmitFeedbackUsecaseHandler(IRepository<FeedbackPost> posts, TimeProvider clock)
    : UsecaseHandlerBase<SubmitFeedbackUsecase, string>
{
    protected override async Task<string> ExecuteAsync(SubmitFeedbackUsecase request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 5000)
        {
            throw new AppException("InvalidFeedback");
        }

        var post = new FeedbackPost
        {
            Id = Guid.NewGuid().ToString("N"),
            Text = request.Text.Trim(),
            CreatedAt = clock.GetUtcNow()
        };
        await posts.AddAsync(post, cancellationToken);
        return post.Id;
    }
}
