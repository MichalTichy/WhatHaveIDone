using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Feedback;

public sealed record ResolveFeedbackUsecase(string Id) : IRequest<bool>;

public sealed class ResolveFeedbackUsecaseHandler(IRepository<FeedbackPost> posts, TimeProvider clock)
    : UsecaseHandlerBase<ResolveFeedbackUsecase, bool>
{
    protected override async Task<bool> ExecuteAsync(ResolveFeedbackUsecase request, CancellationToken cancellationToken)
    {
        var post = await posts.RequireAsync(request.Id, cancellationToken);
        if (post.ResolvedAt is not null)
        {
            return true;
        }

        post.ResolvedAt = clock.GetUtcNow();
        await posts.UpdateAsync(post, cancellationToken);
        return true;
    }
}
