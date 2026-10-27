using MediatR;
using Shared.Infrastructure.CurrentUserProvider;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Clients;

public sealed record SaveClientUsecase(string? Id, ClientModel Input) : IRequest<string>;

public sealed class SaveClientUsecaseHandler(ICurrentUserIdProvider actor, IRepository<Client> clients, IRepository<UserProfile> profiles)
    : OwnerUsecaseHandlerBase<SaveClientUsecase, string>(actor)
{
    protected override async Task<string> ExecuteAsync(SaveClientUsecase request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Input.Name) || request.Input.Name.Length > 200) throw new AppException("InvalidClient");
        if (request.Id is not null)
        {
            var client = await clients.RequireAsync(Owner, request.Id, cancellationToken);
            OwnedDocuments.CheckVersion(client, request.Input.Version);
            client.Name = request.Input.Name.Trim();
            await clients.UpdateAsync(client, cancellationToken);
            return client.Id;
        }
        var profile = await profiles.RequireAsync(Owner, Owner, cancellationToken);
        var created = new Client { Id = Guid.NewGuid().ToString("N"), OwnerId = Owner, CompatibilityId = profile.AllocateId(), Name = request.Input.Name.Trim() };
        await clients.AddAsync(created, cancellationToken);
        await profiles.UpdateAsync(profile, cancellationToken);
        return created.Id;
    }
}
