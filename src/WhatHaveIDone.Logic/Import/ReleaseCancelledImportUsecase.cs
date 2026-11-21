using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed record ReleaseCancelledImportUsecase(string Id, Guid Lease) : IRequest<bool>;

public sealed class ReleaseCancelledImportUsecaseHandler(IRepository<ImportRun> imports)
    : UsecaseHandlerBase<ReleaseCancelledImportUsecase, bool>
{
    protected override async Task<bool> ExecuteAsync(ReleaseCancelledImportUsecase request, CancellationToken cancellationToken)
    {
        var run = await imports.GetByIdAsync(request.Id, cancellationToken);
        if (run is not { Status: ImportStatus.Cancelled } || run.Lease != request.Lease) return false;
        run.Release();
        await imports.UpdateAsync(run, cancellationToken);
        return true;
    }
}
