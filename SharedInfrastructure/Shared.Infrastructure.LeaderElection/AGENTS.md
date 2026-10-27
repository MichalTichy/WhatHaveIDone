# Shared.Infrastructure.LeaderElection

`ILeaderElection` decides which app instance is the leader, i.e. runs work that must happen once: migrations, seeding, scheduled jobs. `InitializerManager` uses it for initializers with `RunOnlyInLeaderInstance`.

- `CheckIfCurrentInstanceIsLeaderAsync()`
- `WaitForLeaderToBeReadyAsync(timeout)`: non-leaders wait until the leader has finished startup.

`LeaderElectionInstaller` (`ILowPriorityInstaller`, `TryAdd`) registers `LeaderElectionSingleInstance` (always leader) unless the app registers another implementation. For multiple instances use `LeaderElection.Redis`.

## Rules

- Check leadership before running singleton background work (e.g. scheduled jobs), not only in initializers.
