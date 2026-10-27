using Marten;

namespace Shared.Infrastructure.Persistence.Marten.StorageConfiguration;

public interface IStorageConfiguration
{
    void Configure(StoreOptions options);
}
