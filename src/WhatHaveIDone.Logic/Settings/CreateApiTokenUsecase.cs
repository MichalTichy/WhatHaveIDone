using System.Security.Cryptography;
using System.Text;
using MediatR;
using Shared.Infrastructure.CurrentUserProvider;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Settings;

public sealed record CreateApiTokenUsecase(string Name) : IRequest<CreatedApiToken>;
public sealed class CreateApiTokenUsecaseHandler(ICurrentUserIdProvider actor, IRepository<ApiToken> tokens, TimeProvider clock) : OwnerUsecaseHandlerBase<CreateApiTokenUsecase, CreatedApiToken>(actor)
{
    protected override async Task<CreatedApiToken> ExecuteAsync(CreateApiTokenUsecase request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100) throw new AppException("InvalidName");
        var secret = "whid_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var token = new ApiToken { Id = Guid.NewGuid().ToString("N"), OwnerId = Owner, Name = request.Name.Trim(), Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))), Suffix = secret[^6..], CreatedAt = clock.GetUtcNow() };
        await tokens.AddAsync(token, cancellationToken); return new CreatedApiToken(token.Id, secret);
    }
}
