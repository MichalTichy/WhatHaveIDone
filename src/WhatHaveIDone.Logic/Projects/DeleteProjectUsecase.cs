using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Projects;

public sealed record DeleteProjectUsecase(string Id, Guid Version) : IRequest<bool>;

public sealed class DeleteProjectUsecaseHandler(
    IRepository<Project> projects, IReadOnlyRepository<TimeEntry> timeentries, IRepository<UserProfile> profiles)
    : UsecaseHandlerBase<DeleteProjectUsecase, bool>
{
    protected override async Task<bool> ExecuteAsync(DeleteProjectUsecase request, CancellationToken cancellationToken)
    {
        var profile = await profiles.RequireAsync(new UserProfileSpecification(), cancellationToken);
        var project = await projects.RequireAsync(request.Id, cancellationToken);
        Documents.CheckVersion(project, request.Version);
        if (await timeentries.GetBySpecAsync(new ProjectUsageSpecification(project.Id), cancellationToken))
            throw new AppException("ProjectInUse", 409);

        // The profile version guards this check against concurrent reference writers.
        await profiles.UpdateAsync(profile, cancellationToken);
        await projects.DeleteAsync(project, cancellationToken);
        return true;
    }
}
