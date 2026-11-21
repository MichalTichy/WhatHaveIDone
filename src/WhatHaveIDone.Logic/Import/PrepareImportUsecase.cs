using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed record PrepareImportUsecase(string ProtectedToken, TogglImportSource Source, DateOnly? From, DateOnly? Through) : IRequest<string>;

public sealed class PrepareImportUsecaseHandler(IRepository<UserProfile> profiles, IRepository<ImportRun> imports, TimeProvider clock)
    : UsecaseHandlerBase<PrepareImportUsecase, string>
{
    protected override async Task<string> ExecuteAsync(PrepareImportUsecase request, CancellationToken cancellationToken)
    {
        var source = request.Source;
        if (source.Workspaces.Count == 0 || string.IsNullOrEmpty(request.ProtectedToken))
            throw new AppException("InvalidImport");

        var profile = await profiles.RequireAsync(new UserProfileSpecification(), cancellationToken);
        var now = clock.GetUtcNow();
        var existing = await imports.ListAsync(new RetainedImportsSpecification(), cancellationToken);
        if (existing.Any(run => run.IsActive || run.LeaseUntil > now)) throw new AppException("ImportAlreadyActive", 409);

        var zone = source.TimeZone;
        var from = request.From ?? new DateOnly(1970, 1, 1);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime);
        var through = request.Through ?? today;
        if (from > through || through > today) throw new AppException("InvalidInterval");

        var run = new ImportRun
        {
            Id = Guid.NewGuid().ToString("N"),
            ProtectedToken = request.ProtectedToken,
            SourceUserId = source.UserId,
            SourceEmail = source.Email,
            SourceTimeZone = zone,
            SourceProjects = source.Projects,
            TagNames = source.Tags,
            Workspaces = source.Workspaces,
            RequestedFrom = from,
            RequestedThrough = through,
            Status = ImportStatus.AwaitingConfirmation,
            CreatedAt = now,
            UpdatedAt = now
        };
        // Concurrent creation attempts for this tenant contend on the same versioned profile.
        await profiles.UpdateAsync(profile, cancellationToken);
        var expired = existing.Skip(19).Select(previous => previous.Id).ToArray();
        if (expired.Length > 0) await imports.DeleteRangeByIdsAsync(expired, cancellationToken);
        await imports.AddAsync(run, cancellationToken);
        return run.Id;
    }
}
