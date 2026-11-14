using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;

public sealed record ResumeImportUsecase(string Id, string? ProtectedToken) : IRequest<bool>;
public sealed class ResumeImportUsecaseHandler(IRepository<UserProfile> profiles,TimeProvider clock) : UsecaseHandlerBase<ResumeImportUsecase,bool>
{
    protected override async Task<bool> ExecuteAsync(ResumeImportUsecase request,CancellationToken cancellationToken)
    {
        var profile=await profiles.RequireAsync(new UserProfileSpecification(),cancellationToken);
        var run=profile.RequireImport(request.Id);
        if(run.Status!="NeedsAttention")throw new AppException("InvalidImport");
        if(request.ProtectedToken is not null){run.ProtectedToken=request.ProtectedToken;if(run.SourceUserId>0){if(run.Stage!="VerifyProfile")run.ResumeStage=run.Stage;run.Stage="VerifyProfile";}}
        run.Status="Queued";run.MessageCode=null;run.NextAttemptAt=null;run.UpdatedAt=clock.GetUtcNow();run.Version=Guid.NewGuid();await profiles.UpdateAsync(profile,cancellationToken);return true;
    }
}
