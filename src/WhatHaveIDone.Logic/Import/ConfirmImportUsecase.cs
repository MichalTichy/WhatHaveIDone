using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed record ConfirmImportUsecase(string Id, ConfirmImportInput Input) : IRequest<bool>;

public sealed class ConfirmImportUsecaseHandler(IRepository<ImportRun> imports, IRepository<UserProfile> profiles, TimeProvider clock)
    : UsecaseHandlerBase<ConfirmImportUsecase, bool>
{
    protected override async Task<bool> ExecuteAsync(ConfirmImportUsecase request, CancellationToken cancellationToken)
    {
        var run = await imports.RequireAsync(request.Id, cancellationToken);
        if (run.Confirmed) return true;
        if (run.Status != ImportStatus.AwaitingConfirmation) throw new AppException("InvalidImport");

        var ids = request.Input.WorkspaceIds;
        if (ids is null || ids.Count == 0 || ids.Any(id => !run.Workspaces.Any(workspace => workspace.Id == id)))
            throw new AppException("InvalidImport");
        run.Calls = ids.Distinct().Select(id => new ImportCall(id, run.RequestedFrom!.Value, run.RequestedThrough!.Value)).ToList();

        var now = clock.GetUtcNow();
        run.Confirmed = true;
        run.Status = ImportStatus.Queued;
        run.UpdatedAt = now;
        await imports.UpdateAsync(run, cancellationToken);

        var profile = await profiles.RequireAsync(new UserProfileSpecification(), cancellationToken);
        if (profile.OnboardingCompletedAt is null)
        {
            profile.OnboardingCompletedAt = now;
            await profiles.UpdateAsync(profile, cancellationToken);
        }
        return true;
    }
}
