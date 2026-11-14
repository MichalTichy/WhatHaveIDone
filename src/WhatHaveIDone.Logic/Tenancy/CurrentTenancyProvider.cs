using Shared.Infrastructure.CurrentTenancyProvider;
using Shared.Infrastructure.CurrentUserProvider;
using WhatHaveIDone.Logic.Execution;

namespace WhatHaveIDone.Logic.Tenancy;

public sealed class CurrentTenancyProvider(ICurrentUserIdProvider actor) : ICurrentTenancyProvider
{
    private string? systemUser;

    public void SetBackgroundUser(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (systemUser is not null) throw new InvalidOperationException("A background scope already has a user.");
        systemUser = userId;
    }

    public async Task<string> GetUserTenantAsync()
    {
        var userId = systemUser ?? await actor.GetCurrentUserIdAsync();
        return !string.IsNullOrWhiteSpace(userId) ? userId : throw new AppException("Unauthenticated", 401);
    }
}
