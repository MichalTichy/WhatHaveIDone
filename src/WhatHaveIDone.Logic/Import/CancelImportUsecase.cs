using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed record CancelImportUsecase(string Id) : IRequest<string?>;
public sealed class CancelImportUsecaseHandler(IRepository<UserProfile> profiles,  TimeProvider clock) : UsecaseHandlerBase<CancelImportUsecase,string?>
{
    protected override async Task<string?> ExecuteAsync(CancelImportUsecase request,CancellationToken cancellationToken)
    {
        var profile=await profiles.RequireAsync(new UserProfileSpecification(),cancellationToken);
        var run=profile.RequireImport(request.Id);if(!run.IsActive)return null;
        var path=run.FilePath;run.FilePath=null;run.ProtectedToken=null;run.Status="Cancelled";run.UpdatedAt=clock.GetUtcNow();run.NextAttemptAt=null;
        run.Finish();
        run.Version=Guid.NewGuid();await profiles.UpdateAsync(profile,cancellationToken);return path;
    }
}
