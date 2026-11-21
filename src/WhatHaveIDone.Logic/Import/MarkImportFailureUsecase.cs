using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed record MarkImportFailureUsecase(string Id, string Code, DateTimeOffset? NextAttempt) : IRequest<bool>;

public sealed class MarkImportFailureUsecaseHandler(IRepository<ImportRun> imports, TimeProvider clock)
    : UsecaseHandlerBase<MarkImportFailureUsecase, bool>
{
    protected override async Task<bool> ExecuteAsync(MarkImportFailureUsecase request, CancellationToken cancellationToken)
    {
        var run = await imports.RequireAsync(request.Id, cancellationToken);
        if (!run.Status.CanAdvance()) throw new AppException("Conflict", 409);
        run.Status = request.NextAttempt.HasValue ? ImportStatus.Waiting : ImportStatus.NeedsAttention;
        run.MessageCode = request.Code;
        run.NextAttemptAt = request.NextAttempt;
        run.RetryCount++;
        run.UpdatedAt = clock.GetUtcNow();
        await imports.UpdateAsync(run, cancellationToken);
        return true;
    }
}
