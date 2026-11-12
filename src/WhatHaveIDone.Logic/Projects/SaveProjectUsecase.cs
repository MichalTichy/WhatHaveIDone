using System.Text.RegularExpressions;
using MediatR;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Settings;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Projects;

public sealed record SaveProjectUsecase(string? Id, ProjectModel Input) : IRequest<string>;

public sealed class SaveProjectUsecaseHandler(IRepository<UserProfile> profiles, IRepository<Project> projects, IReadOnlyRepository<Client> clients)
    : UsecaseHandlerBase<SaveProjectUsecase, string>
{
    protected override async Task<string> ExecuteAsync(SaveProjectUsecase request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Input.Name) || request.Input.Name.Length > 200 || request.Input.Color is null || !Regex.IsMatch(request.Input.Color, "^#[0-9a-fA-F]{6}$")) throw new AppException("InvalidProject");
        var profile = await profiles.RequireAsync(new UserProfileSpecification(), cancellationToken);
        var project = request.Id is null ? null : await projects.RequireAsync(request.Id, cancellationToken);
        if (project is not null) Documents.CheckVersion(project, request.Input.Version);
        var clientId = request.Input.ClientId ?? project?.ClientId ?? profile.DefaultClientId ?? throw new AppException("InvalidClient");
        await clients.RequireAsync(clientId, cancellationToken);
        var isNew = project is null;
        project ??= new Project { Id = Guid.NewGuid().ToString("N"), CompatibilityId = profile.AllocateId(), Name = request.Input.Name.Trim() };
        project.Name = request.Input.Name.Trim(); project.Color = request.Input.Color; project.Archived = request.Input.Archived;
        project.ClientId = clientId;
        if (isNew)
        {
            await projects.AddAsync(project, cancellationToken);
            await profiles.UpdateAsync(profile, cancellationToken);
        }
        else await projects.UpdateAsync(project, cancellationToken);
        return project.Id;
    }
}
