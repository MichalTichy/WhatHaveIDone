using Marten;
using Shared.Infrastructure.CurrentTenancyProvider;

namespace Shared.Infrastructure.Persistence.Marten.SessionFactory;

public class SessionFactoryWithAlternateTenantSettings(IDocumentStore store, ICurrentTenancyProvider currentTenancyProvider)
    : AppMartenSessionFactory(store, currentTenancyProvider), ISessionFactoryWithAlternateTenantSettings
{
    public virtual async Task<IQuerySession> QuerySessionAsync(string? alternateTenantId, UnitOfWorks.UnitOfWork uow, CancellationToken cancellationToken = default)
    {
        var tenantId = alternateTenantId ?? await CurrentTenancyProvider.GetUserTenantAsync();
        var sessionOptions = await CreateSessionOptionsAsync(tenantId, uow, cancellationToken);

        return Store.QuerySession(sessionOptions);
    }

    public virtual async Task<IDocumentSession> OpenSessionAsync(string? alternateTenantId, UnitOfWorks.UnitOfWork uow, CancellationToken cancellationToken = default)
    {
        var tenantId = alternateTenantId ?? await CurrentTenancyProvider.GetUserTenantAsync();
        var sessionOptions = await CreateSessionOptionsAsync(tenantId, uow, cancellationToken);

        return await Store.OpenSerializableSessionAsync(sessionOptions, cancellationToken);
    }
}
