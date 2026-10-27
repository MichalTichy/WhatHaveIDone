namespace Shared.Infrastructure.CurrentTenancyProvider;

public interface ITenancySwitcher
{
    public Task SwitchTenancyAsync(string userId, string? tenancyOverride);
    Task<string?> GetTenancyOverrideAsync(string userId);
}