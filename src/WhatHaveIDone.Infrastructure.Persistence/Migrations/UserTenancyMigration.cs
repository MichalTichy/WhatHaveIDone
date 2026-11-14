using Npgsql;

namespace WhatHaveIDone.Infrastructure.Persistence.Migrations;

// Runs before Marten applies the new tenant-aware schema, while legacy owner fields still exist.
public static class UserTenancyMigration
{
    public static async Task RunAsync(NpgsqlDataSource source, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(738194260);", connection, transaction);
        await command.ExecuteNonQueryAsync(ct);

        async Task<bool> Exists(string table)
        {
            command.CommandText = "SELECT to_regclass(@table) IS NOT NULL";
            command.Parameters.Clear(); command.Parameters.AddWithValue("table", "tracking." + table);
            return (bool)(await command.ExecuteScalarAsync(ct))!;
        }
        async Task Execute(string sql)
        {
            command.Parameters.Clear(); command.CommandText = sql;
            await command.ExecuteNonQueryAsync(ct);
        }

        if (await Exists("mt_doc_importrun"))
        {
            // Do not discard jobs whose owning profile is missing.
            await Execute("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM tracking.mt_doc_importrun r
                        WHERE NOT EXISTS (SELECT 1 FROM tracking.mt_doc_userprofile p
                            WHERE p.id = COALESCE(r.data->>'OwnerId', r.data->>'ownerId')))
                    THEN RAISE EXCEPTION 'An import has no owning profile; tenancy migration stopped'; END IF;
                END $$;
                WITH runs AS (
                    SELECT COALESCE(r.data->>'OwnerId',r.data->>'ownerId') AS owner,
                        (r.data - 'OwnerId' - 'ownerId') || jsonb_build_object('Version', r.mt_version::text) AS data,
                        ROW_NUMBER() OVER (PARTITION BY COALESCE(r.data->>'OwnerId',r.data->>'ownerId')
                            ORDER BY (r.id = COALESCE(p.data->>'ActiveImportRunId',p.data->>'activeImportRunId')) DESC NULLS LAST,
                                COALESCE(r.data->>'CreatedAt',r.data->>'createdAt') DESC, r.id DESC) AS n
                    FROM tracking.mt_doc_importrun r JOIN tracking.mt_doc_userprofile p
                        ON p.id = COALESCE(r.data->>'OwnerId',r.data->>'ownerId')
                ), compact AS (
                    SELECT owner, n, CASE WHEN n > 1 OR COALESCE(data->>'Status',data->>'status') IN
                        ('Completed','CompletedWithWarnings','Cancelled') THEN data ||
                        '{"ProtectedToken":null,"FilePath":null,"TagNames":{},"Preview":[],"Workspaces":[],"AvailableEmails":[]}'::jsonb
                        ELSE data END AS data FROM runs
                ), grouped AS (
                    SELECT owner, (jsonb_agg(data ORDER BY n))->0 AS current,
                        COALESCE(jsonb_agg(data ORDER BY n) FILTER (WHERE n > 1 AND n <= 20), '[]'::jsonb) AS history
                    FROM compact GROUP BY owner
                )
                UPDATE tracking.mt_doc_userprofile p SET data = p.data ||
                    jsonb_build_object('Import', g.current, 'ImportHistory', g.history)
                FROM grouped g WHERE p.id = g.owner;
                DROP TABLE tracking.mt_doc_importrun;
                """);
        }

        foreach (var name in new[] { "userprofile", "client", "project", "tag", "timeentry" })
        {
            var table = "mt_doc_" + name;
            if (!await Exists(table)) continue;
            // Names are a fixed application-owned list, never caller input.
            await Execute($"""
                ALTER TABLE tracking.{table} ADD COLUMN IF NOT EXISTS tenant_id varchar NOT NULL DEFAULT '*DEFAULT*';
                UPDATE tracking.{table} SET tenant_id = COALESCE(data->>'OwnerId',data->>'ownerId')
                    WHERE tenant_id = '*DEFAULT*' AND COALESCE(data->>'OwnerId',data->>'ownerId') IS NOT NULL;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM tracking.{table} WHERE tenant_id = '*DEFAULT*')
                    THEN RAISE EXCEPTION 'A legacy document has no tenant; migration stopped'; END IF;
                END $$;
                UPDATE tracking.{table} SET data = data - 'OwnerId' - 'ownerId' - 'ActiveImportRunId' - 'activeImportRunId'
                    WHERE data ? 'OwnerId' OR data ? 'ownerId' OR data ? 'ActiveImportRunId' OR data ? 'activeImportRunId';
                """);
        }
        if (await Exists("mt_doc_userprofile"))
            await Execute("""
                UPDATE tracking.mt_doc_userprofile SET data = data || jsonb_build_object('SettingsVersion', mt_version::text)
                    WHERE NOT (data ? 'SettingsVersion' OR data ? 'settingsVersion');
                """);
        if (await Exists("mt_doc_apitoken"))
            await Execute("""
                UPDATE tracking.mt_doc_apitoken SET data = (data - 'OwnerId' - 'ownerId') ||
                    jsonb_build_object('UserId', COALESCE(data->>'UserId',data->>'userId',data->>'OwnerId',data->>'ownerId'))
                    WHERE data ? 'OwnerId' OR data ? 'ownerId';
                """);
        await transaction.CommitAsync(ct);
    }
}
