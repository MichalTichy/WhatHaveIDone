namespace Shared.Infrastructure.UnitOfWork;

public class NoUnitOfWorkExecutor : IUnitOfWorkExecutor
{
    public async Task RunInIsolationFromAmbientUnitOfWorkAsync(Func<Task> workload)
    {
        await workload();
    }

    public void RunInIsolationFromAmbientUnitOfWork(Action workload)
    {
        workload();
    }

    public async Task RunInIsolationFromOtherUnitOfWorkAtSameLevelAsync(Func<Task> workload)
    {
        await workload();
    }

    public Task RunInIsolationFromOtherUnitOfWorkAtSameLevelAsync(Action workload)
    {
        workload();
        return Task.CompletedTask;
    }
}
