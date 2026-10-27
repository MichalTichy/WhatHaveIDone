using Shared.Infrastructure.Persistence.Marten.Repository.Events;
using Shared.Infrastructure.Persistence.Marten.SessionFactory;
using Shared.Infrastructure.Persistence.Marten.UnitOfWorks;

namespace Shared.Infrastructure.Persistence.Marten.Repository.Document;

public class MartenReadEventRepository<T>(ISessionFactoryWithAlternateTenantSettings sessionFactory) : IReadEventRepository<T>
    where T : class
{
    public async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default, string? tenantId = null)
    {
        await using var uow = new UnitOfWorkProvider();
        await using var session = await sessionFactory.QuerySessionAsync(tenantId, uow.Get(), cancellationToken);

        var result = await session.LoadAsync<T>(id, cancellationToken);

        await uow.CommitAsync(cancellationToken);
        return result;
    }
}
