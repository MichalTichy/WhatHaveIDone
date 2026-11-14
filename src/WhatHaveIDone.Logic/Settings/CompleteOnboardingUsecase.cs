using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Settings;

public sealed record CompleteOnboardingUsecase : IRequest<bool>;
public sealed class CompleteOnboardingUsecaseHandler(IRepository<UserProfile> profiles, TimeProvider clock) : UsecaseHandlerBase<CompleteOnboardingUsecase, bool>
{
    protected override async Task<bool> ExecuteAsync(CompleteOnboardingUsecase request, CancellationToken cancellationToken)
    {
        var profile = await profiles.RequireAsync(new UserProfileSpecification(), cancellationToken);
        if (profile.OnboardingCompletedAt.HasValue) return true;
        profile.OnboardingCompletedAt = clock.GetUtcNow(); await profiles.UpdateAsync(profile, cancellationToken); return true;
    }
}
