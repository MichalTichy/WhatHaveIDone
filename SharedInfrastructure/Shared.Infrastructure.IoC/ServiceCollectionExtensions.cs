using Microsoft.Extensions.DependencyInjection;

namespace Shared.Infrastructure.IoC;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSingletonWithCheck<TImplementation>(this IServiceCollection services) where TImplementation : class
    {
        return AddSingletonWithCheck<TImplementation, TImplementation>(services);
    }

    public static IServiceCollection AddSingletonWithCheck<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        if (services.Any(sd => sd.ServiceType == typeof(TService)))
        {
            throw new InvalidOperationException($"Service of type {typeof(TService).FullName} is already registered.");
        }

        return services.AddSingleton<TService, TImplementation>();
    }

    public static IServiceCollection AddTransientWithCheck<TImplementation>(this IServiceCollection services) where TImplementation : class
    {
        return AddTransientWithCheck<TImplementation, TImplementation>(services);
    }

    public static IServiceCollection AddTransientWithCheck<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        if (services.Any(sd => sd.ServiceType == typeof(TService)))
        {
            throw new InvalidOperationException($"Service of type {typeof(TService).FullName} is already registered.");
        }

        return services.AddTransient<TService, TImplementation>();
    }

    public static IServiceCollection AddScopedWithCheck<TImplementation>(this IServiceCollection services) where TImplementation : class
    {
        return AddScopedWithCheck<TImplementation, TImplementation>(services);
    }

    public static IServiceCollection AddScopedWithCheck<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        if (services.Any(sd => sd.ServiceType == typeof(TService)))
        {
            throw new InvalidOperationException($"Service of type {typeof(TService).FullName} is already registered.");
        }

        return services.AddScoped<TService, TImplementation>();
    }
}
