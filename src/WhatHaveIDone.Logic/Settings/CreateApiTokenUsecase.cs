using MediatR;
using Shared.Infrastructure.Authentication.ApiKey;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;

namespace WhatHaveIDone.Logic.Settings;

public sealed record CreateApiTokenUsecase(string Name) : IRequest<CreatedApiToken>;
public sealed class CreateApiTokenUsecaseHandler(ApiKeyManager keys) : UsecaseHandlerBase<CreateApiTokenUsecase, CreatedApiToken>
{
    protected override async Task<CreatedApiToken> ExecuteAsync(CreateApiTokenUsecase request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await keys.CreateAsync(request.Name, cancellationToken);
            return new CreatedApiToken(created.Id, created.Key);
        }
        catch (ArgumentException exception) when (exception.ParamName == "name") { throw new AppException("InvalidName"); }
    }
}
