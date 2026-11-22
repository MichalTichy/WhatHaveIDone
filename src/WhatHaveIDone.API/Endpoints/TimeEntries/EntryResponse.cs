using WhatHaveIDone.API.Models;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Endpoints;

internal static class EntryResponse
{
    public static TimeEntryModel Map(TimeEntry entry) => new()
    {
        Id = entry.Id,
        CompatibilityId = entry.CompatibilityId,
        Version = entry.Version,
        Description = entry.Description,
        ProjectId = entry.ProjectId,
        Tags = entry.Tags,
        Start = entry.Start,
        End = entry.End
    };
}
