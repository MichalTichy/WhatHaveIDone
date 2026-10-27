using System.Collections.Concurrent;

namespace Shared.Infrastructure.CurrentTenancyProvider;

public class TenancySwitcher : ITenancySwitcher
{
    protected ConcurrentDictionary<string, string> TenancyInfo = new();

    public Task<string?> GetTenancyOverrideAsync(string userId)
    {
        return Task.FromResult(TenancyInfo.GetValueOrDefault(userId));
    }

    public Task SwitchTenancyAsync(string userId,string? tenancyOverride)
    {
        if (string.IsNullOrWhiteSpace(tenancyOverride))
        {
            TenancyInfo.TryRemove(userId, out _);
        }
        else
        {
            TenancyInfo.AddOrUpdate(userId, _ => tenancyOverride, (_, _) => tenancyOverride);
        }

        return Task.CompletedTask;
    }
}