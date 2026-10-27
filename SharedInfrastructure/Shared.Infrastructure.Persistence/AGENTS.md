# Shared.Infrastructure.Persistence

Storage-independent persistence abstractions: repositories and specifications. Application code depends on these interfaces. The implementation is [Persistence.Marten](../Shared.Infrastructure.Persistence.Marten/AGENTS.md) (PostgreSQL).

## Key types

| Type | Purpose |
|---|---|
| `IItemWithId` | Every stored document has a string `Id`. |
| `IReadOnlyRepository<T>` | Get by ID(s), get/list by specification, count. |
| `IRepository<T>` | Adds `AddAsync`, `UpdateAsync`, `GetAndUpdateAsync` (optimistic concurrency), `DeleteAsync`... |
| `ISpecification<TItem, TOut>` | Query returning a single result, used with `GetBySpecAsync`. |
| `IListSpecification<TItem, TOut>` | Query returning a list, used with `ListAsync`. |

All repository methods take an optional `tenantId`. When it is omitted, the current tenant is used.

## Usage

Repositories come from DI. Inject `IReadOnlyRepository<T>` for reading and `IRepository<T>` for modifying.

```csharp
var job = await jobRepository.GetRequiredByIdAsync(jobId); // throws when not found
var mails = await mailRepository.ListAsync(new MailsByCustomerSpecification(customerId));

// load and modify with optimistic concurrency; the lambda may be async and may return a value
await jobRepository.GetAndUpdateAsync(jobId, job =>
{
    job.Code = newCode;
});
```

A specification encapsulates one query over `IQueryable<T>` (Marten LINQ):

```csharp
public record MailsByCustomerSpecification(string CustomerId) : IListSpecification<MailDocument, MailListModel>
{
    public async Task<IReadOnlyList<MailListModel>> ApplyAsync(IQueryable<MailDocument> queryable, CancellationToken cancellationToken = default)
    {
        return await queryable
            .Where(m => m.CustomerId == CustomerId)
            .Select(m => new MailListModel { /* mapping */ })
            .ToListAsync(cancellationToken);
    }
}
```

Paging: `Specifications.DotVVM` (DotVVM grids) and `Specifications.Pagination` (other UIs and APIs).

## Rules

- Query through specifications, not through raw sessions, so queries stay testable and reusable. Ideally a specification runs a single database query: project to models and call `ToListAsync` at the end.
- Pass `tenantId` only when there is no current tenant (background jobs) or when you deliberately access another tenant. For documents that are not tenant-specific use `INoTenancyRepository<T>` / `INoTenancyReadOnlyRepository<T>` (project `Persistence.Marten`).

## Suggested use

How applications built on Shared.Infrastructure usually organize persistence and business logic. Follow the app's existing structure when it differs.

- **One business logic project** with the `.Core` or `.Logic` suffix, containing these folders:
  - `Documents`: stored domain objects (see [Persistence.Marten](../Shared.Infrastructure.Persistence.Marten/AGENTS.md#suggested-use) for their shape).
  - `Specifications`: queries.
  - `Models`: DTOs exchanged with the UI/API.
  - `UseCases`: MediatR request + response + handler in one file (`SendMailRequest`, `SendMailResponse`, `SendMailUseCase`), registered with `services.AddMediatR(conf => conf.RegisterServicesFromAssemblyContaining<...>())`.
- **Reading:** UI and use cases read through `IReadOnlyRepository<T>` with specifications that project directly to models, so pages don't load more data than they show.
- **Writing:** use cases modify data with `GetAndUpdateAsync` (or `AddAsync` / `DeleteAsync`).
- **Documents stay inside the business layer.** Use case responses and specifications return models, never documents.
- **Models:** one model per scenario rather than a shared one. Suffixes: `ListModel` for grids/lists (`CustomerListModel`), `DetailModel` for detail/edit, `CreateModel` when creating differs from editing.
- **Complex queries:** when a query needs heavy post-processing that doesn't fit one specification, implement it like a use case, but in a `Queries` folder with `Query` in the names.

## Related projects

- `Persistence.Marten`: implementation, registration, storage configuration, migrations.
- `Persistence.Marten.DDD` / `DDD.Core`: repositories for DDD aggregates with domain events.
- `Marten.TestsBase`: test fixtures and repository mocks.
