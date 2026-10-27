using Shared.Infrastructure.CurrentUserProvider;

namespace Shared.Infrastructure.CurrentTenancyProvider;

public abstract class DefaultSwitchableTenancyProvider(ITenancySwitcher tenancySwitcher, ICurrentUserIdProvider currentUserIdProvider): ICurrentTenancyProvider
{
    public async Task<string> GetUserTenantAsync()
    {

        if (!await CanUserUseTenancySwitchingAsync())
        {
            return await GetUsersOriginalTenantAsync();
        }

        var userId = await currentUserIdProvider.GetCurrentUserIdRequiredAsync();
        return await tenancySwitcher.GetTenancyOverrideAsync(userId)
               ?? 
               await GetUsersOriginalTenantAsync();
    }

    protected abstract Task<bool> CanUserUseTenancySwitchingAsync();
    protected abstract Task<string> GetUsersOriginalTenantAsync();
}