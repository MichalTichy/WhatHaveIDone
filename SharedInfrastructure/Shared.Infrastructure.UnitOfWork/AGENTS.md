# Shared.Infrastructure.UnitOfWork

`IUnitOfWorkExecutor`: runs work isolated from the ambient unit of work (`RunInIsolationFromAmbientUnitOfWorkAsync`) or from sibling units of work (`RunInIsolationFromOtherUnitOfWorkAtSameLevelAsync`), e.g. for background processing or notifications that must not join the current transaction/session.

`UnitOfWorkExecutorInstaller` (`ILowPriorityInstaller`, `TryAdd`) registers the no-op `NoUnitOfWorkExecutor` unless another implementation is registered. `Persistence.Marten` registers the real implementation.

## Rules

- Use it when starting work that outlives or runs in parallel with the current request (fire-and-forget, subscription callbacks), so it doesn't share a Marten session with the request.
