namespace Shared.Infrastructure.CurrentTenancyProvider;

public class CurrentTenancyProviderNoTenancy : ICurrentTenancyProvider
{
    public const string NoTenancyName = "*DEFAULT*";

    public Task<string> GetUserTenantAsync()
    {
        return Task.FromResult(NoTenancyName);
    }
}
