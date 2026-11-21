using Shared.Infrastructure.Persistence.Marten.Repository.Document;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Infrastructure.Persistence.Marten.StorageConfiguration;

namespace Shared.Infrastructure.Authentication.ApiKey.Marten;

public static class MartenApiKeyExtensions
{
    public static IServiceCollection AddMartenApiKeyStorage(this IServiceCollection services)
        => services.AddMartenApiKeyStorage<ApiKeyRecord>();

    public static IServiceCollection AddMartenApiKeyStorage<TRecord>(this IServiceCollection services)
        where TRecord : ApiKeyRecord, new()
    {
        services.AddScoped<IApiKeyStorageService>(provider => new MartenApiKeyStorageService<TRecord>(provider.GetRequiredService<INoTenancyReadOnlyRepository<TRecord>>(),
            provider.GetRequiredService<INoTenancyRepository<TRecord>>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStorageConfiguration>(new ApiKeyStorageConfiguration<TRecord>()));
        return services;
    }
}
