# Shared.Infrastructure.Persistence.Marten

Implements the [Persistence](../Shared.Infrastructure.Persistence/AGENTS.md) repositories on [Marten](https://martendb.io/) (PostgreSQL document database). It also covers storage configuration, multi-tenancy, unit of work, data migrations and optional event sourcing.

## Setup

Register persistence in an [installer](../Shared.Infrastructure.IoC/AGENTS.md):

```csharp
public class PersistenceInstaller : IInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration, bool isProduction)
    {
        services.AddNpgsqlDataSource(configuration.GetConnectionString("DB")!);
        services.AutoDiscoverStorageConfigurations(typeof(DocumentBase).Assembly);
        services.AddSingleton<IMartenLogger, ConsoleMartenLogger>();
        services.AddMartenPostgresPersistence(
            useTenancy: true,
            configureStore: options =>
            {
                options.DisableNpgsqlLogging = true;
            });
    }
}
```

- `AutoDiscoverStorageConfigurations` registers all `IStorageConfiguration` classes from the assembly. Many apps instead register them explicitly in an installer in the project with the documents (`services.AddTransient<IStorageConfiguration, JobStorageConfiguration>()`). Follow the existing convention of the app. At least one must be registered, otherwise `AddMartenPostgresPersistence` throws at startup.
- `useTenancy: true` registers `IRepository<T>` scoped to the current tenant, and requires an `ICurrentTenancyProvider` implementation (project `CurrentTenancyProvider`, usually reading the tenant from the user's claims). With `useTenancy: false`, define documents as `SingleTenanted()`.
- `supportEventSourcing: true` enables the async daemon and `IWriteEventRepository` / `IReadEventRepository<T>`.
- For Application Insights logging, use `MartenLogger` from `Persistence.Marten.ApplicationInsightsTracker` instead of `ConsoleMartenLogger`.
- Database creation, schema changes and migrations run in `DatabaseInitializer` at startup (see [Initializer](../Shared.Infrastructure.Initializer/AGENTS.md)).

## Storage configuration

Each document type has a configuration class inheriting `StorageConfigurationBase<T>` (indexes, duplicated fields, tenancy):

```csharp
public class StorageConfiguration(IMartenLogger? martenLogger) : StorageConfigurationBase<JobDocument>(martenLogger)
{
    protected override void ConfigureInternal(StoreOptions options)
    {
        options.Schema.For<JobDocument>()
            .MultiTenanted()
            .Duplicate(x => x.Code);
    }
}
```

Documents implement `IItemWithId`, and `IVersioned` (from Marten) for optimistic concurrency in `GetAndUpdateAsync`. Apps usually define a common base class:

```csharp
public abstract class DocumentBase : IItemWithId, IVersioned
{
    public required string Id { get; init; }
    public Guid Version { get; set; }
}
```

## Data migrations

Implement `IMartenMigration` (discovered automatically). Migrations run in ascending `Version` order, once per database:

```csharp
public class FillCustomerCodeMigration : IMartenMigration
{
    public int Version => 3;

    public async Task MigrateAsync(IDocumentSession session, CancellationToken ct)
    {
        // modify documents through the session
    }
}
```

## Suggested use

Typical document conventions (follow the app's existing documents first):

- One file per aggregate root. Child objects that belong only to it are defined in the same file below it; types shared with other components go into a models project.
- Aggregate roots inherit the app's `DocumentBase` (above).
- The storage configuration is a nested class `StorageConfiguration` inside the document (some apps keep configurations elsewhere, check existing documents).
- Properties that must not change after creation use `init`; non-nullable ones use `required`.
- Collections are `List<T>`.

```csharp
public class JobDocument : DocumentBase
{
    public required string PlanId { get; init; }
    public required string Code { get; set; }
    public List<JobScheduleItem> Schedule { get; set; } = [];

    public class StorageConfiguration(IMartenLogger? martenLogger) : StorageConfigurationBase<JobDocument>(martenLogger)
    {
        protected override void ConfigureInternal(StoreOptions options)
        {
            options.Schema.For<JobDocument>().MultiTenanted().Duplicate(x => x.Code);
        }
    }
}

public class JobScheduleItem
{
    public DateTime MinimumStartDate { get; set; }
}
```

## Notes

## Unit of work completion

Local adaptation: session factory methods accept an optional cancellation token. Pass the operation token through connection/session opening, transaction initialization, repository calls and commit. A cancelled operation must not start uncancellable database work. This repository does not copy upstream test projects; behavior is exercised through `tests/WhatHaveIDone.Api.Tests` against PostgreSQL.

Create an owning `UnitOfWorkProvider` around an operation. Repository providers join it; only the owner commits or disposes the transaction. `IsOwner` and `Current` expose this distinction. After completion the ambient context is detached, so subsequent operations get a fresh unit of work.

Register callbacks on `provider.Get()` while its `State` is `Active`:

```csharp
await using var provider = new UnitOfWorkProvider();
var unit = provider.Get();
unit.OnBeforeCommit(ct => WriteTransactionalAuditAsync(ct));
unit.OnSuccessfulCommit(ct => NotifyCommittedAsync(ct));
unit.OnRollback(ct => WriteFailedAuditInIndependentTransactionAsync(ct));
await ExecuteOperationAsync();
await provider.CommitAsync(cancellationToken);
```

- `OnBeforeCommit` runs in registration order inside the ambient transaction. Repository writes here participate in the same commit. A callback failure aborts the transaction and preserves the original exception.
- `OnSuccessfulCommit` runs once after the actual database commit, outside the completed ambient unit. Its writes use a new transaction and are not atomic with the original operation. All callbacks run; their failures are reported as an `AggregateException`. `State` remains `Committed`, and rollback callbacks never run for that unit.
- `OnRollback` runs once during owner disposal, after rollback and resource disposal, with no ambient unit. Use a fresh provider for independent failure records. Rollback/disposal errors and callback failures are collected in `CompletionErrors` without masking the operation's original exception. Inspect or log these errors at the operation boundary.
- Completion callbacks receive `CancellationToken.None`, because request cancellation must not suppress completion handling. Apply a separate bounded timeout inside callbacks that perform I/O.
- Call `Abort(exception)` if a handler failure was caught, or `Abort()` for a rejected operation. This prevents accidental commit. `Failure` preserves the first failure. Always dispose the owner, including after a failed commit.
- Commit is idempotent after success. Providers are sequential operation scopes: do not share a unit of work between parallel branches. Dispose providers, rather than disposing the unit returned by `Get()` directly.
- The upstream callback test project is not vendored. Run the application's transaction/concurrency/import integration tests with `dotnet test WhatHaveIDone.slnx`; do not require an absent upstream solution.

- Enums are stored as strings and JSON uses Newtonsoft.Json with `TypeNameHandling.Objects`.
- `INoTenancyRepository<T>` / `INoTenancyReadOnlyRepository<T>` access documents without tenant filtering, regardless of `useTenancy`.
- `IUnitOfWorkExecutor` runs work isolated from the ambient unit of work (e.g. in background processing).
