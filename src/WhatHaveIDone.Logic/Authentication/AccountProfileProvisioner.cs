using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Authentication;

public sealed class AccountProfileProvisioner(IRepository<UserProfile> profiles, IRepository<Client> clients)
{
    public async Task EnsureAsync(string owner, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetBySpecAsync(new OwnedByIdSpecification<UserProfile>(owner, owner), cancellationToken);
        if (profile is not null) return;
        profile = new UserProfile { Id = owner, OwnerId = owner };
        var client = Client.CreateDefault(owner, profile.AllocateId());
        profile.DefaultClientId = client.Id;
        await clients.AddAsync(client, cancellationToken);
        await profiles.AddAsync(profile, cancellationToken);
    }
}
