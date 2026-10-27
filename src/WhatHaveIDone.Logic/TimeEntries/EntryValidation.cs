using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.TimeEntries;

public static class EntryValidation
{
    public static async Task ValidateAsync(EditTimeEntry input, string owner, string? previousProject, IReadOnlyRepository<Project> projects, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (input.Description?.Length > 4000) throw new AppException("InvalidDescription");
        if (input.Tags is null) throw new AppException("InvalidTags");
        if (input.End.HasValue && input.End < input.Start || input.End is null && input.Start > now) throw new AppException("InvalidInterval");
        if (!string.IsNullOrWhiteSpace(input.ProjectId))
        {
            var project = await projects.RequireAsync(owner, input.ProjectId, cancellationToken);
            if (project.Archived && input.ProjectId != previousProject) throw new AppException("ArchivedProject");
        }
    }
}
