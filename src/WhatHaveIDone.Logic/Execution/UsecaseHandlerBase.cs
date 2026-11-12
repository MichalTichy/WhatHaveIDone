using MediatR;
using Shared.Infrastructure.Persistence.Marten.UnitOfWorks;

namespace WhatHaveIDone.Logic.Execution;

public abstract class UsecaseHandlerBase<TRequest, TResult> : IRequestHandler<TRequest, TResult> where TRequest : IRequest<TResult>
{
    public async Task<TResult> Handle(TRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = new UnitOfWorkProvider();
        var result = await ExecuteAsync(request, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    protected abstract Task<TResult> ExecuteAsync(TRequest request, CancellationToken cancellationToken);
}
