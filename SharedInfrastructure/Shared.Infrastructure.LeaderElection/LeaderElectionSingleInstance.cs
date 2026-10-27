namespace Shared.Infrastructure.LeaderElection;

public class LeaderElectionSingleInstance : ILeaderElection
{
    public Task<bool> CheckIfCurrentInstanceIsLeaderAsync(bool giveOthersChanceToBecomeLeaderFirst = false)
    {
        return Task.FromResult(true);
    }

    public Task<bool> WaitForLeaderToBeReadyAsync(TimeSpan timeOut)
    {
        return Task.FromResult(true);
    }

    public void InitReElection()
    {
    }
}
