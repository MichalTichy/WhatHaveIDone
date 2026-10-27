using System.Reflection;
using JasperFx.Events.Daemon;
using Marten;
using Marten.Events.Daemon;
using Marten.Newtonsoft;
using Marten.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Shared.Infrastructure.Persistence.Marten.Migration;
using Shared.Infrastructure.Persistence.Marten.Repository.Document;
using Shared.Infrastructure.Persistence.Marten.Repository.Events;
using Shared.Infrastructure.Persistence.Marten.SessionFactory;
using Shared.Infrastructure.Persistence.Marten.StorageConfiguration;
using Shared.Infrastructure.Persistence.Marten.UnitOfWorks;
using Shared.Infrastructure.Persistence.Repositories;
using Shared.Infrastructure.UnitOfWork;
using Weasel.Core;

namespace Shared.Infrastructure.Persistence.Marten.Extensions;

public static class ServiceCollectionExtensions
{
    [Obsolete("Use AddMartenPostgresPersistence without connectionString parameter instead. You need to register Npgsql datasource (AddNpgsqlDataSource).")]
    public static IServiceCollection AddMartenPostgresPersistence(this IServiceCollection services,
        string connectionString,
        Action<StoreOptions>? configureStore = null,
        Action<JsonSerializerSettings>? configureSerialization = null,
        bool supportEventSourcing = false,
        bool useTenancy = false)
    {
        services.AddNpgsqlDataSource(connectionString);
        return AddMartenPostgresPersistence(services, configureStore, configureSerialization, supportEventSourcing, useTenancy);
    }

    public static IServiceCollection AddMartenPostgresPersistence(this IServiceCollection services,
        Action<StoreOptions>? configureStore = null,
        Action<JsonSerializerSettings>? configureSerialization = null,
        bool supportEventSourcing = false,
        bool useTenancy = false)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var serializer = new JsonNetSerializer
        {
            EnumStorage = EnumStorage.AsString
        };

        serializer.Configure(jsonSerializer =>
        {
            jsonSerializer.TypeNameHandling = TypeNameHandling.Objects;

            jsonSerializer.DateTimeZoneHandling = DateTimeZoneHandling.Unspecified;
            configureSerialization?.Invoke(jsonSerializer);
        });

        var configurationExpression = services.AddMarten((IServiceProvider serviceProvider) =>
        {
            var storageConfigurations = serviceProvider.GetServices<IStorageConfiguration>().ToArray();
            if (storageConfigurations.Length == 0)
            {
                throw new ArgumentException(
                    $"No {nameof(IStorageConfiguration)} found in services. {nameof(AddMartenPostgresPersistence)} must be called after {nameof(IStorageConfiguration)} are registered.");
            }

            var options = new StoreOptions();
            options.Serializer(serializer);

            options.Policies.AllDocumentsAreMultiTenanted();

            foreach (var storageConfiguration in storageConfigurations)
            {
                storageConfiguration.Configure(options);
            }

            configureStore?.Invoke(options);

            return options;
        });

        configurationExpression.UseNpgsqlDataSource();

        configurationExpression.ApplyAllDatabaseChangesOnStartup();
        configurationExpression.AssertDatabaseMatchesConfigurationOnStartup();

        services.Scan(
            selector => selector.FromAssemblies(Assembly.GetCallingAssembly())
                .AddClasses(filter =>
                    filter.AssignableTo(typeof(MartenReadEventRepository<>)))
                .As(typeof(IReadEventRepository<>))
                .WithScopedLifetime());

        services.AddTransient<INoTenancyByDefaultSessionFactory, NoTenancyByDefaultSessionFactory>();
        services.AddTransient<ISessionFactoryWithAlternateTenantSettings, SessionFactoryWithAlternateTenantSettings>();
        services.AddTransient<IAsyncSessionFactory, AppMartenSessionFactory>();

        if (useTenancy)
        {
            services.AddScoped(typeof(IRepository<>), typeof(MartenRepository<>));
            services.AddScoped(typeof(IReadOnlyRepository<>), typeof(MartenRepository<>));
        }
        else
        {
            services.AddScoped(typeof(IRepository<>), typeof(NoTenancyMartenRepository<>));
            services.AddScoped(typeof(IReadOnlyRepository<>), typeof(NoTenancyMartenRepository<>));
        }

        services.AddScoped(typeof(INoTenancyRepository<>), typeof(NoTenancyMartenRepository<>));
        services.AddScoped(typeof(INoTenancyReadOnlyRepository<>), typeof(NoTenancyMartenRepository<>));

        services.AddSingleton<IUnitOfWorkExecutor, UnitOfWorkExecutor>();
        services.AddTransient<IDatabaseMigrator, DatabaseMigrator>();

        if (supportEventSourcing)
        {
            configurationExpression.AddAsyncDaemon(DaemonMode.HotCold);
            services.AddScoped<IWriteEventRepository, MartenWriteEventRepository>();

            services.Scan(
                selector => selector.FromAssemblies(Assembly.GetCallingAssembly())
                    .AddClasses(filter =>
                        filter.AssignableTo(typeof(MartenReadEventRepository<>)))
                    .As(typeof(IReadEventRepository<>))
                    .WithScopedLifetime());
        }

        services.Scan(x => x
            .FromApplicationDependencies()
            .AddClasses(c => c.AssignableTo<IMartenMigration>())
            .AsImplementedInterfaces());

        return services;
    }

    public static void AutoDiscoverStorageConfigurations(this IServiceCollection services, Assembly assembly)
    {
        foreach (var t in assembly.GetTypes()
                     .Where(t => t.IsClass
                                 && !t.IsAbstract
                                 && typeof(IStorageConfiguration).IsAssignableFrom(t)))
        {
            services.AddSingleton(typeof(IStorageConfiguration), t);
        }
    }
}