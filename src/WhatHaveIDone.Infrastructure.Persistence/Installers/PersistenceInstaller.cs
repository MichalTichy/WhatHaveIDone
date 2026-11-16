using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Shared.Infrastructure.CurrentTenancyProvider;
using Shared.Infrastructure.Persistence.Marten.Extensions;
using Shared.Infrastructure.Persistence.Marten.StorageConfiguration;
using WhatHaveIDone.Logic.Tenancy;

using Shared.Infrastructure.IoC;

namespace WhatHaveIDone.Infrastructure.Persistence;

public class PersistenceInstaller : IInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration, bool isProduction)
    {
        var connectionString = configuration.GetConnectionString("DB") ?? throw new InvalidOperationException("Configure ConnectionStrings:DB.");
        services.AddNpgsqlDataSource(connectionString);
        services.AddScoped<CurrentTenancyProvider>();
        services.AddScoped<ICurrentTenancyProvider>(sp => sp.GetRequiredService<CurrentTenancyProvider>());
        services.AddSingleton<IStorageConfiguration, TrackingStorageConfiguration>();
        services.AddMartenPostgresPersistence(useTenancy: true, configureSerialization: json =>
        {
            // Preserve explicit offsets through the JSON reader, including the repeated local DST hour.
            json.DateParseHandling = DateParseHandling.DateTimeOffset;
        });
    }
}
