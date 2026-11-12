using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Settings;

public sealed record SaveSettingsUsecase(SettingsModel Input) : IRequest<bool>;
public sealed class SaveSettingsUsecaseHandler(IRepository<UserProfile> profiles) : UsecaseHandlerBase<SaveSettingsUsecase, bool>
{
    protected override async Task<bool> ExecuteAsync(SaveSettingsUsecase request, CancellationToken cancellationToken)
    {
        if (request.Input.Language is not ("cs" or "en") || request.Input.FirstDayOfWeek is < 0 or > 6 || string.IsNullOrWhiteSpace(request.Input.TimeZone)) throw new AppException("InvalidSettings");
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(request.Input.TimeZone); }
        catch (TimeZoneNotFoundException) { throw new AppException("InvalidSettings"); }
        var profile = await profiles.RequireAsync(new UserProfileSpecification(), cancellationToken);
        if (profile.SettingsVersion != request.Input.Version) throw new AppException("Conflict", 409);
        profile.SettingsVersion = Guid.NewGuid();
        profile.Language = request.Input.Language; profile.TimeZone = request.Input.TimeZone; profile.FirstDayOfWeek = request.Input.FirstDayOfWeek;
        await profiles.UpdateAsync(profile, cancellationToken); return true;
    }
}
