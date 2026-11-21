using Npgsql;

namespace WhatHaveIDone.Infrastructure.Persistence;

public static class ImportQueueMigration
{
    public static async Task ApplyAsync(NpgsqlDataSource database, CancellationToken cancellationToken = default)
    {
        // Keep old run IDs, pending page order, and cursor loop detection across an upgrade.
        await using var command = database.CreateCommand("""
            UPDATE tracking.mt_doc_importrun AS run
            SET data = (data - 'Calls' - 'Confirmed' - 'Lease' - 'LeaseUntil')
                || jsonb_build_object(
                    'Status', CASE WHEN data->>'Status' = 'CompletedWithWarnings'
                        THEN 'Completed' ELSE data->>'Status' END,
                    'PendingCalls', CASE WHEN data->>'Status' IN ('Completed', 'CompletedWithWarnings', 'Cancelled')
                        THEN '[]'::jsonb ELSE COALESCE((
                            SELECT jsonb_agg(call - 'Status' ORDER BY position)
                            FROM jsonb_array_elements(data->'Calls') WITH ORDINALITY AS calls(call, position)
                            WHERE call->>'Status' IS DISTINCT FROM 'Done'
                        ), '[]'::jsonb) END,
                    'ConsumedCursors', CASE WHEN data->>'Status' IN ('Completed', 'CompletedWithWarnings', 'Cancelled')
                        THEN '[]'::jsonb ELSE COALESCE((
                            SELECT jsonb_agg(concat(call->>'WorkspaceId', ':', call->>'FirstId', ':', call->>'FirstRow'))
                            FROM jsonb_array_elements(data->'Calls') AS calls(call)
                            WHERE call->>'Status' = 'Done'
                              AND (call->>'FirstId' IS NOT NULL OR call->>'FirstRow' IS NOT NULL)
                        ), '[]'::jsonb) END
                )
            WHERE data ? 'Calls' OR data->>'Status' = 'CompletedWithWarnings';
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
