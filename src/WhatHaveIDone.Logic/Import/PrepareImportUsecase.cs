using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

// Source adapters supply only validated, server-owned token/file handles; no HTTP model binds these paths.
public sealed record PrepareImportUsecase(string Kind, string? ProtectedToken, string? FilePath = null, string? FileHash = null, string? TimeZone = null,
    List<string>? SourceEmails = null, int TotalRows = 0, bool HasOriginalIds = false, List<ImportPreviewItem>? Preview = null, int PreviewErrors = 0) : IRequest<string>;

public sealed class PrepareImportUsecaseHandler(IRepository<UserProfile> profiles, TimeProvider clock)
    : UsecaseHandlerBase<PrepareImportUsecase, string>
{
    protected override async Task<string> ExecuteAsync(PrepareImportUsecase request, CancellationToken cancellationToken)
    {
        if (request.Kind is not ("Api" or "Csv") || request.Kind == "Api" && string.IsNullOrEmpty(request.ProtectedToken) || request.Kind == "Csv" && (request.FilePath is null || request.TotalRows == 0)) throw new AppException("InvalidImport");
        var profile = await profiles.RequireAsync(new UserProfileSpecification(), cancellationToken);
        if (profile.Import is { IsActive: true }) throw new AppException("ImportAlreadyActive",409);
        var now = clock.GetUtcNow();
        var run = new ImportRun { Id = Guid.NewGuid().ToString("N"), Kind = request.Kind, ProtectedToken = request.ProtectedToken, FilePath = request.FilePath,
            FileHash = request.FileHash, SourceTimeZone = request.TimeZone ?? profile.TimeZone, AvailableEmails = request.SourceEmails ?? [], TotalRows = request.TotalRows, HasOriginalIds = request.HasOriginalIds, Preview = request.Preview ?? [], PreviewErrors = request.PreviewErrors,
            CreatedAt = now, UpdatedAt = now, Status = request.Kind == "Csv" ? "AwaitingConfirmation" : "Queued", Stage = request.Kind == "Csv" ? "Ready" : "Profile", RangeStart = DateTimeOffset.UnixEpoch, RangeEnd = now };
        if (profile.Import is { } previous)
        {
            previous.Finish();
            profile.ImportHistory.Insert(0, previous);
            profile.ImportHistory = profile.ImportHistory.Take(19).ToList();
        }
        profile.Import = run;
        await profiles.UpdateAsync(profile,cancellationToken); return run.Id;
    }
}
