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
        var profiles = await session.Query<UserProfile>().Where(p => p.AnyTenant()).Where(p => p.DefaultClientId == null).ToListAsync(ct);
        foreach (var profile in profiles)
        {
            ct.ThrowIfCancellationRequested();
            var client = Client.CreateDefault(profile.Id, profile.AllocateId());
            profile.DefaultClientId = client.Id;
            var tenant = session.ForTenant(profile.Id);
            tenant.Store(client);
            tenant.Store(profile);
            tenant.Patch<Project>(p => p.ClientId == null).Set(p => p.ClientId, client.Id);
        }
    }
}
