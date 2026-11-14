using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed record ConfirmImportUsecase(string Id, ConfirmImportInput Input) : IRequest<bool>;
public sealed class ConfirmImportUsecaseHandler(IRepository<UserProfile> profiles,  TimeProvider clock) : UsecaseHandlerBase<ConfirmImportUsecase,bool>
{
    protected override async Task<bool> ExecuteAsync(ConfirmImportUsecase request, CancellationToken cancellationToken)
    {
        var profile = await profiles.RequireAsync(new UserProfileSpecification(),cancellationToken);
        var run = profile.RequireImport(request.Id);
        if(run.Confirmed)return true;
        if(run.Status!="AwaitingConfirmation")throw new AppException("InvalidImport");
        if(run.Kind=="Api")
        { if(request.Input.WorkspaceIds is null || request.Input.WorkspaceIds.Count==0 || request.Input.WorkspaceIds.Any(id=>!run.WorkspaceIds.Contains(id)))throw new AppException("InvalidImport"); run.WorkspaceIds=request.Input.WorkspaceIds.Distinct().ToList(); }
        else
        { if(!run.AvailableEmails.Contains(request.Input.SourceEmail,StringComparer.OrdinalIgnoreCase))throw new AppException("InvalidImport"); run.SourceEmail=request.Input.SourceEmail; }
        run.Confirmed=true;run.Stage=run.Kind=="Api"?"Tags":"File";run.Status="Queued";run.UpdatedAt=clock.GetUtcNow();
        profile.OnboardingCompletedAt ??= clock.GetUtcNow();
        run.Version=Guid.NewGuid();await profiles.UpdateAsync(profile,cancellationToken);return true;
    }
}
