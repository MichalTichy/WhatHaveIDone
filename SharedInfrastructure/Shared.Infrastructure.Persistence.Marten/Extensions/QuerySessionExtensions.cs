using Marten;
using Npgsql;

namespace Shared.Infrastructure.Persistence.Marten.Extensions;

public static class QuerySessionExtensionMethods
{
    public static NpgsqlConnection GetConnection(this IQuerySession session)
    {
        return session.Connection ?? throw new ArgumentException("Provided session does not have active connection.");
    }
}
