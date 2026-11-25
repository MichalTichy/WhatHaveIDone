using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Predictions;

public sealed record SavePredictionSettingsUsecase(PredictionSettingsModel Input) : IRequest<PredictionSettingsModel>;

public sealed class SavePredictionSettingsUsecaseHandler(IRepository<UserProfile> profiles)
    : UsecaseHandlerBase<SavePredictionSettingsUsecase, PredictionSettingsModel>
{
    protected override async Task<PredictionSettingsModel> ExecuteAsync(SavePredictionSettingsUsecase request, CancellationToken cancellationToken)
    {
        var profile = await profiles.RequireAsync(new UserProfileSpecification(), cancellationToken);
        if (profile.SettingsVersion != request.Input.Version)
        {
            throw new AppException("Conflict", 409);
        }
        profile.PredictionEnabled = request.Input.Enabled;
        profile.SettingsVersion = Guid.NewGuid();
        await profiles.UpdateAsync(profile, cancellationToken);
        return new PredictionSettingsModel(profile.PredictionEnabled, profile.SettingsVersion);
    }
}
