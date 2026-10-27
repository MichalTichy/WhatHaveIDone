namespace Shared.Infrastructure.Persistence.Marten.Repository.Events;

public interface IReadEventRepository<T>
{
    Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default, string? tenantId = null);
}
