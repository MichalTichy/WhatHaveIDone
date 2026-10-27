using MediatR;
using Shared.Infrastructure.CurrentUserProvider;

namespace WhatHaveIDone.Logic.Execution;

public abstract class OwnerUsecaseHandlerBase<TRequest, TResult>(ICurrentUserIdProvider actor)
    : UsecaseHandlerBase<TRequest, TResult> where TRequest : IRequest<TResult>
{
    protected string Owner { get; private set; } = "";

    protected override async Task ValidateAccessAsync(TRequest request, CancellationToken cancellationToken)
    {
        Owner = await actor.GetCurrentUserIdAsync() ?? throw new AppException("Unauthenticated", 401);
    }
}
