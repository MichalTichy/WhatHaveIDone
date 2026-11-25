using Npgsql;
using Shared.Infrastructure.CurrentTenancyProvider;
using WhatHaveIDone.Logic.Predictions;

namespace WhatHaveIDone.Infrastructure.Persistence;

public sealed class DailyTrackedTimeRepository(NpgsqlDataSource dataSource, ICurrentTenancyProvider tenancy) : IDailyTrackedTimeRepository
{
    // Filter this tenant and range once, then split only the matching intervals into local days.
    // Raw SQL does not inherit Marten's tenant predicate, so it must be explicit here.
    // width_bucket locates the precomputed UTC day boundaries without comparing every interval to every day.
    internal const string Sql = """
        WITH selected AS MATERIALIZED (
            SELECT greatest((data->>'Start')::timestamptz, @from) AS start,
                   least(coalesce((data->>'End')::timestamptz, @now), @now) AS finish
            FROM tracking.mt_doc_timeentry
            WHERE tenant_id = @tenant
              AND (data->>'Start')::timestamptz < @now
              AND (tracking.mt_immutable_timestamptz(data->>'End') IS NULL
                   OR tracking.mt_immutable_timestamptz(data->>'End') > @from)
        )
        SELECT (@dates::date[])[n.i] AS day,
               sum(extract(epoch FROM least(s.finish, (@ends::timestamptz[])[n.i])
                   - greatest(s.start, (@starts::timestamptz[])[n.i])))::double precision
        FROM selected s
        CROSS JOIN LATERAL generate_series(width_bucket(s.start, @starts::timestamptz[]),
            least(cardinality(@dates::date[]), width_bucket(s.finish, @starts::timestamptz[]))) AS n(i)
        WHERE s.start < (@ends::timestamptz[])[n.i] AND s.finish > (@starts::timestamptz[])[n.i]
        GROUP BY day
        ORDER BY day
        """;

    public async Task<IReadOnlyList<DailyTrackedTime>> ListAsync(DailyTrackedTimeSpecification specification, CancellationToken cancellationToken)
    {
        var dates = new List<DateOnly>();
        var starts = new List<DateTimeOffset>();
        var ends = new List<DateTimeOffset>();
        for (var day = specification.From; day <= specification.Through; day = day.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            dates.Add(day);
            starts.Add(WorkCalendarRules.DayStart(day, specification.TimeZone));
            ends.Add(WorkCalendarRules.DayStart(day.AddDays(1), specification.TimeZone));
        }
        await using var command = dataSource.CreateCommand(Sql);
        command.Parameters.AddWithValue("tenant", await tenancy.GetUserTenantAsync());
        command.Parameters.AddWithValue("from", starts[0]);
        var cutoff = specification.Now < ends[^1] ? specification.Now.ToUniversalTime() : ends[^1];
        command.Parameters.AddWithValue("now", cutoff);
        command.Parameters.AddWithValue("dates", dates.ToArray());
        command.Parameters.AddWithValue("starts", starts.ToArray());
        command.Parameters.AddWithValue("ends", ends.ToArray());
        var result = new List<DailyTrackedTime>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new DailyTrackedTime(reader.GetFieldValue<DateOnly>(0), reader.GetDouble(1)));
        }
        return result;
    }
}
