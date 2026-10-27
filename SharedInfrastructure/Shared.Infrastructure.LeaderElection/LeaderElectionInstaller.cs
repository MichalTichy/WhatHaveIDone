using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shared.Infrastructure.IoC;

namespace Shared.Infrastructure.LeaderElection;

public class LeaderElectionInstaller : ILowPriorityInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration, bool isProduction)
    {
        services.TryAddSingleton(typeof(ILeaderElection), typeof(LeaderElectionSingleInstance));
    }
}
