using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed record MarkImportFailureUsecase(string Id,string Owner,Guid Version,string Code,DateTimeOffset? NextAttempt) : IRequest<bool>;
public sealed class MarkImportFailureUsecaseHandler(IRepository<ImportRun> imports,TimeProvider clock) : UsecaseHandlerBase<MarkImportFailureUsecase,bool>
{
    private ImportRun run=null!;
    protected override async Task ValidateAccessAsync(MarkImportFailureUsecase request,CancellationToken cancellationToken)
    {
        // System-only background result; no HTTP endpoint accepts this request.
        run=await imports.RequireAsync(request.Owner,request.Id,cancellationToken);OwnedDocuments.CheckVersion(run,request.Version);
        if(!run.IsActive)throw new AppException("Conflict",409);
    }
    protected override async Task<bool> ExecuteAsync(MarkImportFailureUsecase request,CancellationToken cancellationToken)
    {
        run.Status=request.NextAttempt.HasValue?"Waiting":"NeedsAttention";run.MessageCode=request.Code;run.NextAttemptAt=request.NextAttempt;
        run.RetryCount++;run.UpdatedAt=clock.GetUtcNow();await imports.UpdateAsync(run,cancellationToken);return true;
    }
}
