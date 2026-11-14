using Marten;
using Marten.Newtonsoft;
using Marten.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Npgsql;
using WhatHaveIDone.Infrastructure.Persistence;
using WhatHaveIDone.Infrastructure.Persistence.Migrations;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Api.Tests;

[Collection("postgres")]
public sealed class TenancyMigrationTests(IntegrationFixture fixture)
{
    [Fact]
    public async Task LegacyOwnersAndImportCheckpointsMigrateAtomicallyAndCanBeReadWithTheNewSchemaAsync()
    {
        var original = fixture.App.Services.GetRequiredService<IConfiguration>().GetConnectionString("DB")!;
        var database = "whid_migration_" + Guid.NewGuid().ToString("N");
        var builder = new NpgsqlConnectionStringBuilder(original) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(builder.ConnectionString); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand("CREATE DATABASE " + database, admin)) await create.ExecuteNonQueryAsync();
        builder.Database = database;
        try
        {
            using (var legacy = DocumentStore.For(options =>
            {
                options.Connection(builder.ConnectionString); options.DatabaseSchemaName = "tracking";
                options.Serializer(new JsonNetSerializer());
                options.Schema.For<UserProfile>().SingleTenanted().UseOptimisticConcurrency(true);
                options.Schema.For<Project>().SingleTenanted().UseOptimisticConcurrency(true);
                options.Schema.For<Client>().SingleTenanted().UseOptimisticConcurrency(true);
                options.Schema.For<TimeEntry>().SingleTenanted().UseOptimisticConcurrency(true);
                options.Schema.For<Tag>().SingleTenanted().UseOptimisticConcurrency(true);
                options.Schema.For<ApiToken>().SingleTenanted().UseOptimisticConcurrency(true);
                options.Schema.For<ImportRun>().SingleTenanted().UseOptimisticConcurrency(true);
            }))
            {
                await legacy.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
                await using var seed = legacy.LightweightSession();
                seed.Store(new UserProfile { Id = "alice", RunningEntryId = "entry", DefaultClientId = "client" });
                seed.Store(new UserProfile { Id = "bob", DefaultClientId = "bob-client" });
                seed.Store(new Project { Id = "project", CompatibilityId = 42, Name = "Imported project", ClientId = "client" });
                seed.Store(new Client { Id = "client", CompatibilityId = 41, Name = "Personal", IsDefault = true });
                seed.Store(new TimeEntry { Id = "entry", CompatibilityId = 43, ProjectId = "project", Start = DateTimeOffset.UnixEpoch });
                seed.Store(new Tag { Id = "tag", CompatibilityId = 44, Name = "Review", NormalizedName = "REVIEW" });
                seed.Store(new ApiToken { Id = "key", UserId = "alice", Name = "Existing key", Hash = "synthetic-hash", Suffix = "suffix" });
                seed.Store(new ImportRun { Id = "run", Kind = "Api", Status = "Waiting", Page = 7, ProtectedToken = "synthetic-encrypted-placeholder", NextAttemptAt = DateTimeOffset.UtcNow.AddHours(1), CreatedAt = DateTimeOffset.UtcNow });
                await seed.SaveChangesAsync();
                await using var sql = new NpgsqlConnection(builder.ConnectionString); await sql.OpenAsync();
                foreach (var table in new[] { "userprofile", "project", "client", "timeentry", "tag", "apitoken", "importrun" })
                {
                    await using var command = new NpgsqlCommand($"UPDATE tracking.mt_doc_{table} SET data = data || jsonb_build_object('OwnerId', CASE WHEN id='bob' THEN 'bob' ELSE 'alice' END)", sql);
                    await command.ExecuteNonQueryAsync();
                }
                await using var oldProfile = new NpgsqlCommand("UPDATE tracking.mt_doc_userprofile SET data=data-'SettingsVersion'-'Import'-'ImportHistory'", sql);
                await oldProfile.ExecuteNonQueryAsync();
                await using var orphan = new NpgsqlCommand("INSERT INTO tracking.mt_doc_project (id,data) SELECT 'orphan',data-'OwnerId' FROM tracking.mt_doc_project WHERE id='project'", sql);
                await orphan.ExecuteNonQueryAsync();
            }
            await using var source = NpgsqlDataSource.Create(builder.ConnectionString);
            await Assert.ThrowsAsync<PostgresException>(() => UserTenancyMigration.RunAsync(source, CancellationToken.None));
            await using (var verifyRollback = await source.OpenConnectionAsync())
            {
                await using var checkRollback = new NpgsqlCommand("SELECT to_regclass('tracking.mt_doc_importrun') IS NOT NULL", verifyRollback);
                Assert.True((bool)(await checkRollback.ExecuteScalarAsync())!);
                await using var repair = new NpgsqlCommand("DELETE FROM tracking.mt_doc_project WHERE id='orphan'", verifyRollback);
                await repair.ExecuteNonQueryAsync();
            }
            await UserTenancyMigration.RunAsync(source, CancellationToken.None);
            using var migrated = DocumentStore.For(options =>
            {
                options.Connection(builder.ConnectionString); options.Serializer(new JsonNetSerializer());
                new TrackingStorageConfiguration().Configure(options);
            });
            await migrated.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
            await UserTenancyMigration.RunAsync(source, CancellationToken.None);
            await using var alice = migrated.QuerySession("alice"); await using var bob = migrated.QuerySession("bob");
            var profile = (await alice.LoadAsync<UserProfile>("alice"))!;
            Assert.Equal("entry", profile.RunningEntryId); Assert.NotEqual(Guid.Empty, profile.SettingsVersion);
            Assert.Equal("Waiting", profile.Import!.Status); Assert.Equal(7, profile.Import.Page);
            Assert.Equal("alice", (await alice.Query<UserProfile>().Where(p => p.Import != null && p.Import.Status == "Waiting").SingleAsync()).Id);
            Assert.Equal("synthetic-encrypted-placeholder", profile.Import.ProtectedToken); Assert.NotEqual(Guid.Empty, profile.Import.Version);
            Assert.Equal(42, (await alice.LoadAsync<Project>("project"))!.CompatibilityId);
            Assert.Equal("project", (await alice.LoadAsync<TimeEntry>("entry"))!.ProjectId);
            Assert.Equal("alice", (await alice.LoadAsync<ApiToken>("key"))!.UserId);
            Assert.Null(await bob.LoadAsync<Project>("project")); Assert.Null(await bob.LoadAsync<UserProfile>("alice"));
            await using var connection = await source.OpenConnectionAsync();
            await using var check = new NpgsqlCommand("SELECT to_regclass('tracking.mt_doc_importrun') IS NULL", connection);
            Assert.True((bool)(await check.ExecuteScalarAsync())!);
        }
        finally
        {
            await using var drop = new NpgsqlCommand("DROP DATABASE " + database + " WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
}
