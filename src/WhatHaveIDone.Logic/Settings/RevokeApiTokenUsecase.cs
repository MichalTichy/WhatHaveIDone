using MediatR;
using Shared.Infrastructure.Authentication.ApiKey;
using WhatHaveIDone.Logic.Execution;

namespace WhatHaveIDone.Logic.Settings;

public sealed record RevokeApiTokenUsecase(string Id) : IRequest<bool>;
public sealed class RevokeApiTokenUsecaseHandler(ApiKeyManager keys) : UsecaseHandlerBase<RevokeApiTokenUsecase, bool>
{
    protected override async Task<bool> ExecuteAsync(RevokeApiTokenUsecase request, CancellationToken cancellationToken)
        => await keys.RevokeAsync(request.Id, cancellationToken) ? true : throw new AppException("NotFound", 404);
}
