using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Shared.Infrastructure.CurrentTenancyProvider;
using Shared.Infrastructure.Persistence.Marten.Extensions;
using Shared.Infrastructure.Persistence.Marten.StorageConfiguration;

namespace WhatHaveIDone.Infrastructure.Persistence;

public static class PersistenceRegistration
{
    public static IServiceCollection AddTrackingPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddNpgsqlDataSource(connectionString);
        services.AddSingleton<ICurrentTenancyProvider, CurrentTenancyProviderNoTenancy>();
        services.AddSingleton<IStorageConfiguration, TrackingStorageConfiguration>();
        services.AddMartenPostgresPersistence(useTenancy: false, configureSerialization: json =>
        {
            // Preserve explicit offsets through the JSON reader, including the repeated local DST hour.
            json.DateParseHandling = DateParseHandling.DateTimeOffset;
        });
        return services;
    }
}
