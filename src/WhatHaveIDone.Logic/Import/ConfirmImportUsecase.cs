using MediatR;
using Shared.Infrastructure.CurrentUserProvider;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed record ConfirmImportUsecase(string Id, ConfirmImportInput Input) : IRequest<bool>;
public sealed class ConfirmImportUsecaseHandler(ICurrentUserIdProvider actor, IRepository<UserProfile> profiles, IRepository<ImportRun> imports, TimeProvider clock) : OwnerUsecaseHandlerBase<ConfirmImportUsecase,bool>(actor)
{
    protected override async Task<bool> ExecuteAsync(ConfirmImportUsecase request, CancellationToken cancellationToken)
    {
        var run = await imports.RequireAsync(Owner, request.Id,cancellationToken);
        if(run.Confirmed)return true;
        if(run.Status!="AwaitingConfirmation")throw new AppException("InvalidImport");
        if(run.Kind=="Api")
        { if(request.Input.WorkspaceIds is null || request.Input.WorkspaceIds.Count==0 || request.Input.WorkspaceIds.Any(id=>!run.WorkspaceIds.Contains(id)))throw new AppException("InvalidImport"); run.WorkspaceIds=request.Input.WorkspaceIds.Distinct().ToList(); }
        else
        { if(!run.AvailableEmails.Contains(request.Input.SourceEmail,StringComparer.OrdinalIgnoreCase))throw new AppException("InvalidImport"); run.SourceEmail=request.Input.SourceEmail; }
        run.Confirmed=true;run.Stage=run.Kind=="Api"?"Tags":"File";run.Status="Queued";run.UpdatedAt=clock.GetUtcNow();
        var profile = await profiles.RequireAsync(Owner,Owner,cancellationToken); profile.OnboardingCompletedAt ??= clock.GetUtcNow();
        await imports.UpdateAsync(run,cancellationToken);await profiles.UpdateAsync(profile,cancellationToken);return true;
    }
}
