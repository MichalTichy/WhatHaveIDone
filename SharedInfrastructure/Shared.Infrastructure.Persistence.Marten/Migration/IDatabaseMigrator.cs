namespace Shared.Infrastructure.Persistence.Marten.Migration;

public interface IDatabaseMigrator
{
    Task<bool> EnsureMigrationHistoryTableAsync(CancellationToken ct = default);
    Task SaveLastMigrationToHistoryAsync(CancellationToken ct = default);
    Task RunMigrationsAsync(CancellationToken ct = default);
}
