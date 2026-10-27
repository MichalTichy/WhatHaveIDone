using MediatR;
using Shared.Infrastructure.CurrentUserProvider;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed record CancelImportUsecase(string Id) : IRequest<string?>;
public sealed class CancelImportUsecaseHandler(ICurrentUserIdProvider actor, IRepository<UserProfile> profiles, IRepository<ImportRun> imports, TimeProvider clock) : OwnerUsecaseHandlerBase<CancelImportUsecase,string?>(actor)
{
    protected override async Task<string?> ExecuteAsync(CancelImportUsecase request,CancellationToken cancellationToken)
    {
        var run=await imports.RequireAsync(Owner,request.Id,cancellationToken);if(!run.IsActive)return null;
        var path=run.FilePath;run.FilePath=null;run.ProtectedToken=null;run.Status="Cancelled";run.UpdatedAt=clock.GetUtcNow();run.NextAttemptAt=null;
        var profile=await profiles.RequireAsync(Owner,Owner,cancellationToken);if(profile.ActiveImportRunId==run.Id)profile.ActiveImportRunId=null;
        await imports.UpdateAsync(run,cancellationToken);await profiles.UpdateAsync(profile,cancellationToken);return path;
    }
}
