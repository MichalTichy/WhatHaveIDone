using Shared.Infrastructure.Persistence.Marten.SessionFactory;
using Shared.Infrastructure.Persistence.Marten.UnitOfWorks;

namespace Shared.Infrastructure.Persistence.Marten.Repository.Events;

public class MartenWriteEventRepository(IAsyncSessionFactory sessionFactory) : IWriteEventRepository
{
    public async Task AddEventAsync<TEvent>(string streamId, TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        if (@event is null)
        {
            throw new ArgumentNullException(nameof(@event));
        }

        await using var uow = new UnitOfWorkProvider();
        await using var session = await sessionFactory.OpenSessionAsync(uow.Get(), cancellationToken);

        session.Events.Append(streamId, @event);

        await session.SaveChangesAsync(cancellationToken);
        await uow.CommitAsync(cancellationToken);
    }
}
