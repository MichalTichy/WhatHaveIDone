using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed record ClaimImportUsecase(string Id) : IRequest<ImportRun?>;

public sealed class ClaimImportUsecaseHandler(IRepository<ImportRun> imports, TimeProvider clock)
    : UsecaseHandlerBase<ClaimImportUsecase, ImportRun?>
{
    protected override async Task<ImportRun?> ExecuteAsync(ClaimImportUsecase request, CancellationToken cancellationToken)
    {
        var run = await imports.GetByIdAsync(request.Id, cancellationToken);
        var now = clock.GetUtcNow();
        if (run is null || !run.Status.CanAdvance() || run.NextAttemptAt > now || run.LeaseUntil > now) return null;

        run.Lease = Guid.NewGuid();
        run.LeaseUntil = now.AddMinutes(2);
        run.Status = ImportStatus.Running;
        run.NextAttemptAt = null;
        run.UpdatedAt = now;
        await imports.UpdateAsync(run, cancellationToken);
        return run;
    }
}
