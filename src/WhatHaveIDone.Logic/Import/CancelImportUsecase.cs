using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed record CancelImportUsecase(string Id) : IRequest<bool>;

public sealed class CancelImportUsecaseHandler(IRepository<ImportRun> imports, TimeProvider clock)
    : UsecaseHandlerBase<CancelImportUsecase, bool>
{
    protected override async Task<bool> ExecuteAsync(CancelImportUsecase request, CancellationToken cancellationToken)
    {
        var run = await imports.RequireAsync(request.Id, cancellationToken);
        if (!run.IsActive) return true;

        run.Status = ImportStatus.Cancelled;
        run.UpdatedAt = clock.GetUtcNow();
        run.Finish();
        await imports.UpdateAsync(run, cancellationToken);
        return true;
    }
}
