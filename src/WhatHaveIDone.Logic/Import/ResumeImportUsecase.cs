using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed record ResumeImportUsecase(string Id, string? ProtectedToken, long? SourceUserId) : IRequest<bool>;

public sealed class ResumeImportUsecaseHandler(IRepository<ImportRun> imports, TimeProvider clock)
    : UsecaseHandlerBase<ResumeImportUsecase, bool>
{
    protected override async Task<bool> ExecuteAsync(ResumeImportUsecase request, CancellationToken cancellationToken)
    {
        var run = await imports.RequireAsync(request.Id, cancellationToken);
        if (run.Status != ImportStatus.NeedsAttention) throw new AppException("InvalidImport");
        if (request.ProtectedToken is not null)
        {
            if (request.SourceUserId != run.SourceUserId) throw new AppException("ImportToken");
            run.ProtectedToken = request.ProtectedToken;
        }

        run.Status = ImportStatus.Queued;
        run.MessageCode = null;
        run.NextAttemptAt = null;
        run.RetryCount = 0;
        run.UpdatedAt = clock.GetUtcNow();
        await imports.UpdateAsync(run, cancellationToken);
        return true;
    }
}
