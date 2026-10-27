using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Infrastructure.IoC;

public interface IInstaller
{
    public void Install(IServiceCollection services, IConfiguration configuration, bool isProduction);
}

public interface IHighPriorityInstaller : IInstaller
{
}

public interface ILowPriorityInstaller : IInstaller
{
}
