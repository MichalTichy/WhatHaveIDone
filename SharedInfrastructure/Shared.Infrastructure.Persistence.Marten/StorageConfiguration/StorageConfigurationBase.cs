using Marten;
using Marten.Schema.Identity;

namespace Shared.Infrastructure.Persistence.Marten.StorageConfiguration;

public abstract class StorageConfigurationBase<T>(IMartenLogger? martenLogger) : IStorageConfiguration where T : IItemWithId
{
    public virtual void Configure(StoreOptions options)
    {
        ConfigureInternal(options);

        options.Policies.ForAllDocuments(m =>
        {
            if (m.IdType == typeof(Guid))
            {
                m.IdStrategy = new NoOpIdGeneration();
            }
        });

        if (martenLogger != null)
        {
            options.Logger(martenLogger);
        }
    }

    protected abstract void ConfigureInternal(StoreOptions options);
}
