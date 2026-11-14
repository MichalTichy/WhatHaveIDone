using MediatR;
using Shared.Infrastructure.CurrentUserProvider;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Settings;

public sealed record RevokeApiTokenUsecase(string Id) : IRequest<bool>;
public sealed class RevokeApiTokenUsecaseHandler(ICurrentUserIdProvider actor, IRepository<ApiToken> tokens, TimeProvider clock) : UsecaseHandlerBase<RevokeApiTokenUsecase, bool>
{
    protected override async Task<bool> ExecuteAsync(RevokeApiTokenUsecase request, CancellationToken cancellationToken)
    {
        var token = await tokens.RequireAsync(request.Id, cancellationToken);
        if (token.UserId != await actor.GetCurrentUserIdRequiredAsync()) throw new AppException("NotFound", 404);
        token.RevokedAt ??= clock.GetUtcNow(); await tokens.UpdateAsync(token, cancellationToken); return true;
    }
}
