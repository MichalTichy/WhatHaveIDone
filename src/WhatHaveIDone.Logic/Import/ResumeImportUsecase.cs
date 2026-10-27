using MediatR;
using Shared.Infrastructure.CurrentUserProvider;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed record ResumeImportUsecase(string Id, string? ProtectedToken) : IRequest<bool>;
public sealed class ResumeImportUsecaseHandler(ICurrentUserIdProvider actor,IRepository<ImportRun> imports,TimeProvider clock) : OwnerUsecaseHandlerBase<ResumeImportUsecase,bool>(actor)
{
    protected override async Task<bool> ExecuteAsync(ResumeImportUsecase request,CancellationToken cancellationToken)
    {
        var run=await imports.RequireAsync(Owner,request.Id,cancellationToken);
        if(run.Status!="NeedsAttention")throw new AppException("InvalidImport");
        if(request.ProtectedToken is not null){run.ProtectedToken=request.ProtectedToken;if(run.SourceUserId>0){if(run.Stage!="VerifyProfile")run.ResumeStage=run.Stage;run.Stage="VerifyProfile";}}
        run.Status="Queued";run.MessageCode=null;run.NextAttemptAt=null;run.UpdatedAt=clock.GetUtcNow();await imports.UpdateAsync(run,cancellationToken);return true;
    }
}
