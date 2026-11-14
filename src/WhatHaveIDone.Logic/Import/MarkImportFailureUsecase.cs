using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed record MarkImportFailureUsecase(string Id,Guid Version,string Code,DateTimeOffset? NextAttempt) : IRequest<bool>;
public sealed class MarkImportFailureUsecaseHandler(IRepository<UserProfile> profiles,TimeProvider clock) : UsecaseHandlerBase<MarkImportFailureUsecase,bool>
{
    protected override async Task<bool> ExecuteAsync(MarkImportFailureUsecase request,CancellationToken cancellationToken)
    {
        // System-only background result; no HTTP endpoint accepts this request.
        var profile=await profiles.RequireAsync(new UserProfileSpecification(),cancellationToken);
        var run=profile.RequireImport(request.Id);if(run.Version!=request.Version)throw new AppException("Conflict",409);
        if(!run.IsActive)throw new AppException("Conflict",409);
        run.Status=request.NextAttempt.HasValue?"Waiting":"NeedsAttention";run.MessageCode=request.Code;run.NextAttemptAt=request.NextAttempt;
        run.RetryCount++;run.UpdatedAt=clock.GetUtcNow();run.Version=Guid.NewGuid();await profiles.UpdateAsync(profile,cancellationToken);return true;
    }
}
