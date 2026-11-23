using System.Security.Cryptography;
using System.Text.Json;
using JasperFx;
using MediatR;
using Npgsql;
using Shared.Infrastructure.Persistence.Marten.Repository.Document;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Import;
using WhatHaveIDone.Logic.Tenancy;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Import;

public sealed class ImportWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ImportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessNextAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Import scheduler could not discover due work."); }
            await Task.Delay(TimeSpan.FromSeconds(2), clock, stoppingToken);
        }
    }

    public async Task ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var discovery = scopes.CreateAsyncScope();
        var jobs = await discovery.ServiceProvider.GetRequiredService<INoTenancyReadOnlyRepository<ImportRun>>()
            .ListAsync(new RunnableImportsSpecification(clock.GetUtcNow()), cancellationToken);
        await Parallel.ForEachAsync(jobs.DistinctBy(run => run.TenantId),
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken }, ProcessAsync);
    }

    private async ValueTask ProcessAsync(ImportRun candidate, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<CurrentTenancyProvider>().SetBackgroundUser(candidate.TenantId);
        var sender = services.GetRequiredService<ISender>();
        try
        {
            var run = await services.GetRequiredService<IReadOnlyRepository<ImportRun>>()
                .GetByIdAsync(candidate.Id, cancellationToken);
            if (run is null || !run.Status.CanAdvance() || run.NextAttemptAt > clock.GetUtcNow()) return;

            try
            {
                using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                readTimeout.CancelAfter(TimeSpan.FromSeconds(60));
                var batch = await services.GetRequiredService<TogglImportClient>().FetchAsync(run, readTimeout.Token);
                var apply = new ApplyImportBatchUsecase(run.Id, batch.Entries,
                    batch.Continuation);

                for (var attempt = 0; ; attempt++)
                {
                    try
                    {
                        await sender.Send(apply, cancellationToken);
                        break;
                    }
                    catch (Exception exception) when (attempt < 2 && IsConcurrentWrite(exception))
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100), clock, cancellationToken);
                    }
                }
            }
            catch (TogglImportException exception)
            {
                if (exception.NextAttempt is null)
                    logger.LogWarning("Import run {ImportId} paused with source error {ErrorCode}.", run.Id, exception.Code);
                await sender.Send(new MarkImportFailureUsecase(run.Id, exception.Code, exception.NextAttempt), cancellationToken);
            }
            catch (AppException exception) when (exception.Code != "Conflict")
            {
                logger.LogWarning("Import run {ImportId} paused with error {ErrorCode}.", run.Id, exception.Code);
                await sender.Send(new MarkImportFailureUsecase(run.Id, exception.Code, null), cancellationToken);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                logger.LogWarning("Import run {ImportId} paused because an imported ID already exists.", run.Id);
                await sender.Send(new MarkImportFailureUsecase(run.Id, "ImportIdCollision", null), cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
                exception is HttpRequestException or JsonException or IOException or OperationCanceledException or FormatException or OverflowException or CryptographicException)
            {
                logger.LogWarning("Import run {ImportId} paused after {FailureType} while reading Toggl.", run.Id, exception.GetType().Name);
                await sender.Send(new MarkImportFailureUsecase(run.Id, "ImportSource", null), cancellationToken);
            }
        }
        catch (AppException exception) when (exception.Code is "Conflict" or "NotFound") { }
        catch (Exception exception) when (IsConcurrentWrite(exception)) { }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { logger.LogError(exception, "Import run {ImportId} could not advance.", candidate.Id); }
    }

    private static bool IsConcurrentWrite(Exception exception)
        => exception is ConcurrencyException or PostgresException { SqlState: PostgresErrorCodes.SerializationFailure };
}
