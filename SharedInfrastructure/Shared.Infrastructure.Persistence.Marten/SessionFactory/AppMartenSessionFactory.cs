using System.Data;
using Marten;
using Marten.Services;
using Npgsql;
using Shared.Infrastructure.CurrentTenancyProvider;

namespace Shared.Infrastructure.Persistence.Marten.SessionFactory;

public class AppMartenSessionFactory(IDocumentStore store, ICurrentTenancyProvider currentTenancyProvider) : IAsyncSessionFactory
{
    protected readonly ICurrentTenancyProvider CurrentTenancyProvider = currentTenancyProvider;
    protected readonly IDocumentStore Store = store;

    public virtual async Task<IQuerySession> QuerySessionAsync(UnitOfWorks.UnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var tenantId = await GetTenantIdAsync();
        var sessionOptions = await CreateSessionOptionsAsync(tenantId, unitOfWork, cancellationToken);

        return Store.QuerySession(sessionOptions);
    }

    public virtual async Task<IDocumentSession> OpenSessionAsync(UnitOfWorks.UnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var tenantId = await GetTenantIdAsync();
        var sessionOptions = await CreateSessionOptionsAsync(tenantId, unitOfWork, cancellationToken);

        return await Store.OpenSerializableSessionAsync(sessionOptions, cancellationToken);
    }

    protected virtual async Task<SessionOptions> CreateSessionOptionsAsync(string? tenantId, UnitOfWorks.UnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        await unitOfWork.EnsureInitializedAsync(GetOpenConnectionAsync, GetIsolationLevel(), cancellationToken);
        var options = SessionOptions.ForTransaction(unitOfWork.Transaction!);

        options.IsolationLevel = GetIsolationLevel();

        if (tenantId != null)
        {
            options.TenantId = tenantId;
        }

        return options;
    }

    private async Task<NpgsqlConnection> GetOpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = Store.Storage.Database.CreateConnection();
        try { await connection.OpenAsync(cancellationToken); }
        catch { await connection.DisposeAsync(); throw; }
        return connection;
    }

    private IsolationLevel GetIsolationLevel()
    {
        return IsolationLevel.ReadCommitted;
    }

    private async Task<string> GetTenantIdAsync()
    {
        return await CurrentTenancyProvider.GetUserTenantAsync();
    }
}
