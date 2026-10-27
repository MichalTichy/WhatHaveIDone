# Shared.Infrastructure.IoC

Organizes DI registrations into **installers**: small classes that register a group of services and are discovered automatically at startup.

## Setup

In `Program.cs`, discover and run all installers from the application's assemblies and from all referenced `Shared.Infrastructure.*` assemblies:

```csharp
var dllPrefix = "MyApp"; // assembly name prefix of the application's projects
InstallerDiscovery.RunInstallersFromAllReferencedAssemblies(
    builder.Services, builder.Configuration, builder.Environment.IsProduction(), dllPrefix);
```

Assemblies with the prefix and `Shared.Infrastructure*` are force-loaded first, so installers in referenced but not-yet-loaded assemblies are found too.

## Writing an installer

Installers live in the `Installers` folder of the project whose services they register:

```csharp
using Shared.Infrastructure.IoC;

public class ConfigurationInstaller : IInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration, bool isProduction)
    {
        services.Configure<AppConfiguration>(configuration.GetSection("App"));
        services.AddScoped<OptimizationScenarioFactory>();
    }
}
```

- Installers are created with `Activator.CreateInstance`, so they need a public parameterless constructor and no DI.
- Order: `IHighPriorityInstaller` runs first, then `IInstaller`, then `ILowPriorityInstaller`. Use the priority interfaces only when a registration really depends on another one.

## Notes

- **Installers in Shared.Infrastructure projects run in every application that references the project.** When adding an installer to a shared project, make sure it is safe for all consumers: register only what the project needs, and bind options without requiring configuration the app might not have. If the app should choose, expose an `Add<Feature>()` extension method instead.
- `AddSingletonWithCheck`, `AddScopedWithCheck` and `AddTransientWithCheck` throw when the service is already registered. Use them to catch accidental double registration.
- Startup tasks (migrations, seeding) are not installers. Use [initializers](../Shared.Infrastructure.Initializer/AGENTS.md).
