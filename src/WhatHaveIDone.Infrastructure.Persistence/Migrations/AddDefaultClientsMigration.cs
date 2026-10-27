using Marten;
using Marten.Patching;
using Shared.Infrastructure.Persistence.Marten.Migration;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Infrastructure.Persistence.Migrations;

public sealed class AddDefaultClientsMigration : IMartenMigration
{
    public int Version => 1;

    public async Task MigrateAsync(IDocumentSession session, CancellationToken ct)
    {
        var profiles = await session.Query<UserProfile>().Where(p => p.DefaultClientId == null).ToListAsync(ct);
        foreach (var profile in profiles)
        {
            ct.ThrowIfCancellationRequested();
            var client = Client.CreateDefault(profile.Id, profile.AllocateId());
            profile.DefaultClientId = client.Id;
            session.Store(client);
            session.Store(profile);
            session.Patch<Project>(p => p.OwnerId == profile.Id && p.ClientId == null).Set(p => p.ClientId, client.Id);
        }
    }
}
