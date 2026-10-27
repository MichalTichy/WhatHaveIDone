using Ardalis.GuardClauses;
using Marten;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;
using Shared.Infrastructure.Initializer;
using Shared.Infrastructure.Persistence.Marten.Migration;
using System.Reflection;
using JasperFx;
using Weasel.Core;

namespace Shared.Infrastructure.Persistence.Marten;

public class DatabaseInitializer(IDocumentStore store, ILogger<DatabaseInitializer> logger, NpgsqlDataSource dataSource, IDatabaseMigrator databaseMigration) : InitializerBase
{
    public override int Priority => int.MaxValue;
    public override InitializerTrigger Trigger => InitializerTrigger.OnStartup;

    public override bool RunOnlyInLeaderInstance => true;

    protected override async Task RunInitializationLogicAsync()
    {
        var documentTypes = store.Options.AllKnownDocumentTypes();
        foreach (var documentType in documentTypes)
        {
            logger.LogInformation("Found document {name}", documentType.Alias);
        }


        var isNewDbCreated = await Policy
            .Handle<NpgsqlException>()
            .WaitAndRetryAsync(6, _ => TimeSpan.FromSeconds(5))
            .ExecuteAsync(CreateDatabaseIfItDoesNotExistsAsync);

        await ApplySchemaChangesAsync();
        await ApplyMigrationsAsync(isNewDbCreated);
    }

    /// <summary>
    ///     Temporary hack while following issue is not resolved:
    ///     https://github.com/JasperFx/marten/issues/2786
    /// </summary>
    private async Task<bool> CreateDatabaseIfItDoesNotExistsAsync()
    {
        // Parse the connection string and replace the database name with 'postgres' temporarily

        var builder = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString);

        if (string.IsNullOrWhiteSpace(builder.Password))
        {
            // Use reflection to get the private field 'settings'
            var settingsField = typeof(NpgsqlDataSource).GetProperty("Settings", BindingFlags.NonPublic | BindingFlags.Instance);
            var value = settingsField?.GetValue(dataSource);
            if (value is NpgsqlConnectionStringBuilder settings)
            {
                builder = new NpgsqlConnectionStringBuilder(settings.ConnectionString);
            }
            else
            {
                throw new ArgumentException("Unable to determine password to DB.");
            }
        }

        var newDatabaseName = builder.Database;
        Guard.Against.NullOrWhiteSpace(newDatabaseName, nameof(newDatabaseName));
        builder.Database = "postgres";

        try
        {
            await using var connection = new NpgsqlConnection(builder.ToString());
            await connection.OpenAsync();
            // Check if the database already exists
            await using var checkDbCommand = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = '{newDatabaseName}'", connection);
            var databaseExists = await checkDbCommand.ExecuteScalarAsync() != null;
            if (databaseExists)
            {
                return false;
            }

            logger.LogWarning("Database does not exists. Creating new one.");
            // Create new database
            await using var createDbCommand = new NpgsqlCommand($"CREATE DATABASE \"{newDatabaseName}\"", connection);
            await createDbCommand.ExecuteNonQueryAsync();
            return true;
        }
        catch (NpgsqlException ex)
        {
            logger.LogCritical(ex, "Error while ensuring that database exists.");
            throw;
        }
    }

    private async Task ApplySchemaChangesAsync()
    {
        logger.LogInformation("Applying database schema changes.");

        await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync(AutoCreate.CreateOrUpdate);

        await store.Options.Tenancy.Default.Database.ApplyAllConfiguredChangesToDatabaseAsync(AutoCreate.CreateOrUpdate);
    }

    private async Task ApplyMigrationsAsync(bool isNewDb)
    {
        await databaseMigration.EnsureMigrationHistoryTableAsync();

        if (isNewDb)
        {
            logger.LogInformation("Saving the latest migration version to the history, because database was created just now and model is assumed to be up to date.");
            await databaseMigration.SaveLastMigrationToHistoryAsync();
        }
        else
        {
            logger.LogInformation("Applying database migrations.");
            await databaseMigration.RunMigrationsAsync();
        }
    }
}
