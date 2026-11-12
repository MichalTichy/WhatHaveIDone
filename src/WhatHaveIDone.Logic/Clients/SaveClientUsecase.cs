using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Clients;

public sealed record SaveClientUsecase(string? Id, ClientModel Input) : IRequest<string>;

public sealed class SaveClientUsecaseHandler(IRepository<Client> clients, IRepository<UserProfile> profiles)
    : UsecaseHandlerBase<SaveClientUsecase, string>
{
    protected override async Task<string> ExecuteAsync(SaveClientUsecase request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Input.Name) || request.Input.Name.Length > 200) throw new AppException("InvalidClient");
        if (request.Id is not null)
        {
            var client = await clients.RequireAsync(request.Id, cancellationToken);
            Documents.CheckVersion(client, request.Input.Version);
            client.Name = request.Input.Name.Trim();
            await clients.UpdateAsync(client, cancellationToken);
            return client.Id;
        }
        var profile = await profiles.RequireAsync(new UserProfileSpecification(), cancellationToken);
        var created = new Client { Id = Guid.NewGuid().ToString("N"), CompatibilityId = profile.AllocateId(), Name = request.Input.Name.Trim() };
        await clients.AddAsync(created, cancellationToken);
        await profiles.UpdateAsync(profile, cancellationToken);
        return created.Id;
    }
}
