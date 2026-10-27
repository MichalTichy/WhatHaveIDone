using Microsoft.Extensions.Configuration;

namespace Shared.Infrastructure.Initializer;

public interface IInitializer
{
    /// <summary>
    ///     The Initializers are first grouped by Trigger and than Sorted by priority.
    /// </summary>
    int Priority { get; }

    InitializerTrigger Trigger { get; }

    bool RunOnlyInLeaderInstance { get; }
    string Name { get; }
    public Task InitializeAsync(IConfiguration configuration);
}
