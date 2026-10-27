using Marten;
using Shared.Infrastructure.CurrentTenancyProvider;

namespace Shared.Infrastructure.Persistence.Marten.SessionFactory;

public class NoTenancyByDefaultSessionFactory(IDocumentStore store, ICurrentTenancyProvider currentTenancyProvider)
    : SessionFactoryWithAlternateTenantSettings(store, currentTenancyProvider), INoTenancyByDefaultSessionFactory
{
    public override async Task<IDocumentSession> OpenSessionAsync(string? alternateTenantId, UnitOfWorks.UnitOfWork uow, CancellationToken cancellationToken = default)
    {
        if (alternateTenantId != null)
        {
            return await base.OpenSessionAsync(alternateTenantId, uow, cancellationToken);
        }

        return await OpenSessionAsync(uow, cancellationToken);
    }

    public override async Task<IQuerySession> QuerySessionAsync(string? alternateTenantId, UnitOfWorks.UnitOfWork uow, CancellationToken cancellationToken = default)
    {
        if (alternateTenantId != null)
        {
            return await base.QuerySessionAsync(alternateTenantId, uow, cancellationToken);
        }

        return await QuerySessionAsync(uow, cancellationToken);
    }

    public override async Task<IDocumentSession> OpenSessionAsync(UnitOfWorks.UnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var sessionOptions = await CreateSessionOptionsAsync(null, unitOfWork, cancellationToken);

        return await Store.OpenSerializableSessionAsync(sessionOptions, cancellationToken);
    }

    public override async Task<IQuerySession> QuerySessionAsync(UnitOfWorks.UnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var sessionOptions = await CreateSessionOptionsAsync(null, unitOfWork, cancellationToken);

        return Store.QuerySession(sessionOptions);
    }
}
