namespace Shared.Infrastructure.LeaderElection;

public interface ILeaderElection
{
    public Task<bool> CheckIfCurrentInstanceIsLeaderAsync(bool giveOthersChanceToBecomeLeaderFirst = false);
    public Task<bool> WaitForLeaderToBeReadyAsync(TimeSpan timeOut);
    void InitReElection();
}
