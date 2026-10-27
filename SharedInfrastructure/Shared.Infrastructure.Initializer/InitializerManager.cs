using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.LeaderElection;

namespace Shared.Infrastructure.Initializer;

public class InitializerManager(ILogger<InitializerManager> logger, IServiceProvider serviceProvider, ILeaderElection leaderElection, IConfiguration configuration)
{
    private static readonly SemaphoreSlim InitializersRunSemaphore = new(1);

    private readonly IServiceProvider serviceProvider = serviceProvider.CreateScope().ServiceProvider;
    private readonly TaskCompletionSource<bool> initializersFinishedCompletionSource = new();
    public bool IsInitialized => HaveAllInitializationPhasesFinished();
    public ICollection<InitializerTrigger> ExecutedTriggers { get; private set; } = new List<InitializerTrigger>();

    public async Task RunAllInitializersAsync(InitializerTrigger trigger, params InitializerTrigger[] triggers)
    {
        triggers = triggers.Prepend(trigger).ToArray();

        CheckThatInitializersWereNotAlreadyRun(triggers);
        try
        {
            await InitializersRunSemaphore.WaitAsync();
            CheckThatInitializersWereNotAlreadyRun(triggers);

            foreach (var triggerToAdd in triggers)
            {
                ExecutedTriggers.Add(triggerToAdd);
            }

            var allInitializers = serviceProvider.GetServices<IInitializer>();

            var isLeader = await WaitToBecomeLeaderOrForLeaderToBeReadyAsync();

            if (!isLeader)
            {
                allInitializers = allInitializers.Where(t => t.RunOnlyInLeaderInstance == false);
            }


            var selectedInitializers = allInitializers
                .Where(t => triggers.Contains(t.Trigger))
                .OrderBy(t => t.Trigger).ThenByDescending(t => t.Priority)
                .ToArray();

            logger.LogInformation("Running {initializerCount} initializers.", selectedInitializers.Length);
            foreach (var initializer in selectedInitializers)
            {
                logger.LogInformation("Running initializer {initializerName} - priority {priority}.", initializer.Name, initializer.Priority);

                await initializer.InitializeAsync(configuration);

                logger.LogInformation("Initializer {initializerName} finished.", initializer.Name);
            }

            OnInitializationCompleted();
        }
        catch (Exception e)
        {
            initializersFinishedCompletionSource.SetException(e);
            throw;
        }
        finally
        {
            InitializersRunSemaphore.Release();
        }
    }

    private async Task<bool> WaitToBecomeLeaderOrForLeaderToBeReadyAsync()
    {
        bool isReady;
        bool isLeader;
        do
        {
            isLeader = await leaderElection.CheckIfCurrentInstanceIsLeaderAsync(true);
            var timeout = TimeSpan.FromSeconds(2);
            if (!isLeader)
            {
                logger.LogInformation("Waiting for leader instance to finish its initialization.");

                var isLeaderReady = await leaderElection.WaitForLeaderToBeReadyAsync(timeout);
                if (!isLeaderReady)
                {
                    logger.LogWarning("Leader is not ready yet.");
                }
                else
                {
                    logger.LogInformation("Leader is ready, starting initialization.");
                }
            }

            var doWeNeedToWaitForLeader = !isLeader || !await leaderElection.WaitForLeaderToBeReadyAsync(timeout);
            isReady = !doWeNeedToWaitForLeader;
        } while (!isReady);

        return isLeader;
    }

    private void OnInitializationCompleted()
    {
        leaderElection.InitReElection();

        if (HaveAllInitializationPhasesFinished())
        {
            initializersFinishedCompletionSource.SetResult(true);
        }
    }

    public Task WaitForAllInitializersToFinishAsync()
    {
        return initializersFinishedCompletionSource.Task;
    }

    private bool HaveAllInitializationPhasesFinished()
    {
        return Enum.GetValues<InitializerTrigger>().All(ExecutedTriggers.Contains);
    }

    private void CheckThatInitializersWereNotAlreadyRun(InitializerTrigger[] triggers)
    {
        if (!ExecutedTriggers.Any(triggers.Contains))
        {
            return;
        }

        var alreadyRunInitializerTrigger = ExecutedTriggers.First(triggers.Contains);
        throw new NotSupportedException($"Initializers with trigger {alreadyRunInitializerTrigger} was already run!");
    }
}
