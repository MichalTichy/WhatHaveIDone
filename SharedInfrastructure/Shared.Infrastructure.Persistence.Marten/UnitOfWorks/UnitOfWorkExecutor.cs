using Shared.Infrastructure.UnitOfWork;

namespace Shared.Infrastructure.Persistence.Marten.UnitOfWorks;

public class UnitOfWorkExecutor : IUnitOfWorkExecutor
{
    public async Task RunInIsolationFromAmbientUnitOfWorkAsync(Func<Task> workload)
    {
        using (ExecutionContext.SuppressFlow())
        {
            await workload.Invoke();
        }
    }

    public void RunInIsolationFromAmbientUnitOfWork(Action workload)
    {
        using (ExecutionContext.SuppressFlow())
        {
            workload.Invoke();
        }
    }

    public async Task RunInIsolationFromOtherUnitOfWorkAtSameLevelAsync(Func<Task> workload)
    {
        await workload();
    }

    public async Task RunInIsolationFromOtherUnitOfWorkAtSameLevelAsync(Action workload)
    {
        await Task.Run(workload);
    }
}
