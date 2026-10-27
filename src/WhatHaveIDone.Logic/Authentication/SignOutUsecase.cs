using MediatR;
using Shared.Infrastructure.CurrentUserProvider;
using WhatHaveIDone.Logic.Execution;

namespace WhatHaveIDone.Logic.Authentication;

public sealed record SignOutUsecase : IRequest<bool>;
public sealed class SignOutUsecaseHandler(ICurrentUserIdProvider actor, IAccountAccess accounts) : OwnerUsecaseHandlerBase<SignOutUsecase, bool>(actor)
{
    protected override async Task<bool> ExecuteAsync(SignOutUsecase request, CancellationToken cancellationToken)
    { await accounts.SignOutAsync(cancellationToken); return true; }
}
