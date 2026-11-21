using Marten;
using Shared.Infrastructure.Persistence.Marten.StorageConfiguration;

namespace Shared.Infrastructure.Authentication.ApiKey.Marten;

internal sealed class ApiKeyStorageConfiguration<TRecord> : IStorageConfiguration where TRecord : ApiKeyRecord
{
    public void Configure(StoreOptions options)
        => options.Schema.For<TRecord>().SingleTenanted().UseOptimisticConcurrency(true)
            .Metadata(metadata => metadata.Version.MapTo(key => key.Version))
            .Index(key => key.UserId).UniqueIndex(key => key.Hash);
}
