namespace Shared.Infrastructure.UnitOfWork;

public interface IUnitOfWorkExecutor
{
    Task RunInIsolationFromAmbientUnitOfWorkAsync(Func<Task> workload);
    void RunInIsolationFromAmbientUnitOfWork(Action workload);
    Task RunInIsolationFromOtherUnitOfWorkAtSameLevelAsync(Func<Task> workload);
    Task RunInIsolationFromOtherUnitOfWorkAtSameLevelAsync(Action workload);
}
