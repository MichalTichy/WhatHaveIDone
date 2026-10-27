using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Infrastructure.IoC;

public static class InstallerDiscovery
{
    public static void RunInstallersFromAllReferencedAssemblies(IServiceCollection serviceCollection,
        IConfiguration configuration,
        bool isProduction,
        string dllPrefix,
        bool forceLoadAssemblies = true,
        FullAssemblyLoader? forceAssemblyLoader = null)
    {
        if (forceLoadAssemblies)
        {
            forceAssemblyLoader ??= FullAssemblyLoader.Default;
            forceAssemblyLoader.Load(dllPrefix);
            forceAssemblyLoader.Load("Shared.Infrastructure");
        }

        var type = typeof(IInstaller);
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var highPriorityInstallers = new List<IInstaller>();
        var normalPriorityInstallers = new List<IInstaller>();
        var lowPriorityInstallers = new List<IInstaller>();

        foreach (var assembly in assemblies.Where(t => 
                                     (t.GetName().Name?.StartsWith(dllPrefix) ?? true)
                                     || (t.GetName().Name?.StartsWith("Shared.Infrastructure") ?? true)))
        {
            foreach (var installerType in assembly.GetTypes().Where(p => type.GetTypeInfo().IsAssignableFrom(p) && p.IsClass && !p.IsAbstract))
            {
                if (Activator.CreateInstance(installerType) is not IInstaller installer)
                {
                    continue;
                }

                if (installer is IHighPriorityInstaller)
                {
                    highPriorityInstallers.Add(installer);
                }
                else if (installer is ILowPriorityInstaller)
                {
                    lowPriorityInstallers.Add(installer);
                }
                else
                {
                    normalPriorityInstallers.Add(installer);
                }
            }
        }

        foreach (var installer in highPriorityInstallers)
        {
            installer.Install(serviceCollection, configuration, isProduction);
        }

        foreach (var installer in normalPriorityInstallers)
        {
            installer.Install(serviceCollection, configuration, isProduction);
        }

        foreach (var installer in lowPriorityInstallers)
        {
            installer.Install(serviceCollection, configuration, isProduction);
        }
    }
}
