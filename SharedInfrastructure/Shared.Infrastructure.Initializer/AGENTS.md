# Shared.Infrastructure.Initializer

Runs startup tasks (**initializers**) such as database migrations, seeding or cache warm-up, in a defined order and optionally only on the leader instance.

## Setup

```csharp
var dllPrefix = "MyApp";
builder.Services.AddAllInitializers(dllPrefix);

// ...

var app = builder.Build();

// right before app.Run()
await app.Services.GetRequiredService<InitializerManager>()
    .RunAllInitializersAsync(InitializerTrigger.OnStartup, InitializerTrigger.OnApplicationReady);

app.Run();
```

`AddAllInitializers` registers every `IInitializer` from the application's assemblies (by prefix) **and from all loaded `Shared.Infrastructure*` assemblies**. For example, `Persistence.Marten` contributes `DatabaseInitializer`, which creates the database and applies schema changes and migrations. `InitializerManager` and `ILeaderElection` are registered by installers (see [IoC](../Shared.Infrastructure.IoC/AGENTS.md)). Without `LeaderElection.Redis`, a single-instance leader election is used.

## Writing an initializer

Initializers live in the `Initializers` folder. Inherit from `InitializerBase` or implement `IInitializer`:

```csharp
using Shared.Infrastructure.Initializer;

public class SeedDataInitializer(IRepository<SettingsDocument> settings) : InitializerBase
{
    public override int Priority => 0;
    public override InitializerTrigger Trigger => InitializerTrigger.OnApplicationReady;
    public override bool RunOnlyInLeaderInstance => true;

    protected override async Task RunInitializationLogicAsync()
    {
        // ...
    }
}
```

- **Order**: initializers are grouped by `Trigger` (`OnStartup` before `OnApplicationReady`). Within a trigger, **higher `Priority` runs first**.
- **`RunOnlyInLeaderInstance`**: when the app runs in multiple instances, only the leader runs these initializers. Use it for migrations and seeding. Non-leader instances wait until the leader is ready.
- Each initializer runs in a DI scope and receives dependencies through its constructor.
