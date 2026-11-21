using System.Text.Json;
using JasperFx;
using MediatR;
using Npgsql;
using Shared.Infrastructure.Persistence.Repositories;
using Shared.Infrastructure.Persistence.Marten.Repository.Document;
using WhatHaveIDone.Logic.Tenancy;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Import;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Import;

public sealed class ImportWorker(IServiceScopeFactory scopes,TimeProvider clock,ILogger<ImportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using (var scope = scopes.CreateAsyncScope())
        {
            var files = await scope.ServiceProvider.GetRequiredService<INoTenancyReadOnlyRepository<UserProfile>>().ListAsync(new ActiveImportFilesSpecification(), stoppingToken);
            scope.ServiceProvider.GetRequiredService<ImportStorage>().CleanOrphans(files, clock.GetUtcNow(), stoppingToken);
        }
        while(!stoppingToken.IsCancellationRequested)
        {
            try{await ProcessNextAsync(stoppingToken);}
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception exception){logger.LogError(exception,"Import processing could not advance.");}
            await Task.Delay(TimeSpan.FromSeconds(2),clock,stoppingToken);
        }
    }
    public async Task ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var scope=scopes.CreateAsyncScope();
        var services=scope.ServiceProvider;
        var profile=(await services.GetRequiredService<INoTenancyReadOnlyRepository<UserProfile>>().ListAsync(new RunnableImportsSpecification(clock.GetUtcNow()),cancellationToken)).FirstOrDefault();
        if(profile?.Import is not { } run)return;
        var version=run.Version;var path=run.FilePath;
        try
        {
            var batch=run.Kind=="Api"?await services.GetRequiredService<TogglImportClient>().FetchAsync(run,cancellationToken):await services.GetRequiredService<ImportStorage>().ReadBatchAsync(run,cancellationToken);
            for(var attempt=0;attempt<3;attempt++)
            {
                try
                {
                    await using var writeScope=scopes.CreateAsyncScope();
                    writeScope.ServiceProvider.GetRequiredService<CurrentTenancyProvider>().SetBackgroundUser(profile.Id);
                    var next=JsonSerializer.Deserialize<ImportRun>(JsonSerializer.Serialize(batch.Next,ImportStorage.JsonOptions),ImportStorage.JsonOptions)!;next.Version=version;
                    await writeScope.ServiceProvider.GetRequiredService<ISender>().Send(new ApplyImportBatchUsecase(next,batch.Projects,batch.Entries),cancellationToken);
                    if(batch.Next.Status is "Completed" or "CompletedWithWarnings")services.GetRequiredService<ImportStorage>().Delete(path);
                    return;
                }
                catch(Exception exception)when(attempt<2&&(exception is ConcurrencyException||exception is PostgresException{SqlState:"40001"}))
                { await Task.Delay(TimeSpan.FromMilliseconds(100),clock,cancellationToken); }
            }
        }
        catch(AppException exception)when(exception.Code=="Conflict") { } // Cancellation/confirmation won the job version; do not apply a stale batch.
        catch(ConcurrencyException) { }
        catch(PostgresException exception)when(exception.SqlState=="40001") { }
        catch(TogglImportException exception){await MarkFailureAsync(run.Id,profile.Id,version,exception.Code,exception.NextAttempt,cancellationToken);}
        catch(AppException exception){await MarkFailureAsync(run.Id,profile.Id,version,exception.Code,null,cancellationToken);}
        catch(PostgresException exception)when(exception.SqlState=="23505"){await MarkFailureAsync(run.Id,profile.Id,version,"ImportIdCollision",null,cancellationToken);}
        catch(OperationCanceledException)when(cancellationToken.IsCancellationRequested){throw;}
        catch(Exception exception)when(exception is HttpRequestException or JsonException or IOException or TaskCanceledException or FormatException or System.Security.Cryptography.CryptographicException)
        {await MarkFailureAsync(run.Id,profile.Id,version,"ImportSource",null,cancellationToken);}
    }
    private async Task MarkFailureAsync(string id,string owner,Guid version,string code,DateTimeOffset? next,CancellationToken cancellationToken)
    {
        await using var scope=scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentTenancyProvider>().SetBackgroundUser(owner);
        try{await scope.ServiceProvider.GetRequiredService<ISender>().Send(new MarkImportFailureUsecase(id,version,code,next),cancellationToken);}
        catch(AppException exception)when(exception.Code=="Conflict") { }
        catch(ConcurrencyException) { }
    }
}
