using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Projects;

public sealed class ProjectsSpecification : IListSpecification<Project, ProjectModel>
{
    public async Task<IReadOnlyList<ProjectModel>> ApplyAsync(IQueryable<Project> queryable, CancellationToken cancellationToken = default)
        => await queryable.OrderBy(p => p.Name).Select(p => new ProjectModel
        { Id = p.Id, CompatibilityId = p.CompatibilityId, Version = p.Version, Name = p.Name, Color = p.Color, ClientId = p.ClientId, Archived = p.Archived }).ToListAsync(cancellationToken);
}
