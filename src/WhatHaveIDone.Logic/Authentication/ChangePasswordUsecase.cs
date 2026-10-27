using MediatR;
using Shared.Infrastructure.CurrentUserProvider;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;

namespace WhatHaveIDone.Logic.Authentication;

public sealed record ChangePasswordUsecase(ChangePasswordInput Input) : IRequest<bool>;
public sealed class ChangePasswordUsecaseHandler(ICurrentUserIdProvider actor, IAccountAccess accounts) : OwnerUsecaseHandlerBase<ChangePasswordUsecase, bool>(actor)
{
    protected override async Task<bool> ExecuteAsync(ChangePasswordUsecase request, CancellationToken cancellationToken)
    { await accounts.ChangePasswordAsync(request.Input, cancellationToken); return true; }
}
