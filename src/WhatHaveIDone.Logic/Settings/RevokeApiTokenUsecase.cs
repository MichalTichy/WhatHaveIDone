using MediatR;
using Shared.Infrastructure.CurrentUserProvider;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Settings;

public sealed record RevokeApiTokenUsecase(string Id) : IRequest<bool>;
public sealed class RevokeApiTokenUsecaseHandler(ICurrentUserIdProvider actor, IRepository<ApiToken> tokens, TimeProvider clock) : OwnerUsecaseHandlerBase<RevokeApiTokenUsecase, bool>(actor)
{
    protected override async Task<bool> ExecuteAsync(RevokeApiTokenUsecase request, CancellationToken cancellationToken)
    {
        var token = await tokens.RequireAsync(Owner, request.Id, cancellationToken);
        token.RevokedAt ??= clock.GetUtcNow(); await tokens.UpdateAsync(token, cancellationToken); return true;
    }
}
