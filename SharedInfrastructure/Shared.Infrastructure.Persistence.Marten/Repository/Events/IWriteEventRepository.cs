namespace Shared.Infrastructure.Persistence.Marten.Repository.Events;

public interface IWriteEventRepository
{
    Task AddEventAsync<TEvent>(string streamId, TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class;
}
