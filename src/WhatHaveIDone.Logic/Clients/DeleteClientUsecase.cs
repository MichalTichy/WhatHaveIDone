using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Clients;

public sealed record DeleteClientUsecase(string Id, Guid Version) : IRequest<bool>;

public sealed class DeleteClientUsecaseHandler(
    IRepository<Client> clients, IReadOnlyRepository<Project> projects, IRepository<UserProfile> profiles)
    : UsecaseHandlerBase<DeleteClientUsecase, bool>
{
    protected override async Task<bool> ExecuteAsync(DeleteClientUsecase request, CancellationToken cancellationToken)
    {
        var profile = await profiles.RequireAsync(new UserProfileSpecification(), cancellationToken);
        var client = await clients.RequireAsync(request.Id, cancellationToken);
        Documents.CheckVersion(client, request.Version);
        if (profile.DefaultClientId == client.Id)
            throw new AppException("DeleteDefaultClient", 409);
        if (await projects.GetBySpecAsync(new ClientUsageSpecification(client.Id), cancellationToken))
            throw new AppException("ClientInUse", 409);

        // The profile version guards this check against concurrent reference writers.
        await profiles.UpdateAsync(profile, cancellationToken);
        await clients.DeleteAsync(client, cancellationToken);
        return true;
    }
}
