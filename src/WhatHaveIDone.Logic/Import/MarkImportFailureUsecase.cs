using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed record MarkImportFailureUsecase(string Id, Guid Lease, string Code, DateTimeOffset? NextAttempt) : IRequest<bool>;

public sealed class MarkImportFailureUsecaseHandler(IRepository<ImportRun> imports, TimeProvider clock)
    : UsecaseHandlerBase<MarkImportFailureUsecase, bool>
{
    protected override async Task<bool> ExecuteAsync(MarkImportFailureUsecase request, CancellationToken cancellationToken)
    {
        var run = await imports.RequireAsync(request.Id, cancellationToken);
        run.RequireLease(request.Lease);
        run.Status = request.NextAttempt.HasValue ? ImportStatus.Waiting : ImportStatus.NeedsAttention;
        run.MessageCode = request.Code;
        run.NextAttemptAt = request.NextAttempt;
        run.RetryCount++;
        run.UpdatedAt = clock.GetUtcNow();
        if (request.NextAttempt is null)
        {
            var index = run.Calls.FindIndex(call => call.Status == ImportCallStatus.Todo);
            if (index >= 0) run.Calls[index] = run.Calls[index] with { Status = ImportCallStatus.Error };
        }
        run.Release();
        await imports.UpdateAsync(run, cancellationToken);
        return true;
    }
}
