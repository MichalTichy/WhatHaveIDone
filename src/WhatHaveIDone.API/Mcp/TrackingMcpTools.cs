using System.ComponentModel;
using MediatR;
using ModelContextProtocol.Server;
using Shared.Infrastructure.CurrentUserProvider;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Projects;
using WhatHaveIDone.Logic.Reporting;
using WhatHaveIDone.Logic.Tags;
using WhatHaveIDone.Logic.TimeEntries;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Mcp;

[McpServerToolType]
public sealed class TrackingMcpTools(ICurrentUserIdProvider actor, ISender sender, IReadOnlyRepository<TimeEntry> entries,
    IReadOnlyRepository<Project> projects, IReadOnlyRepository<Tag> tags, IReadOnlyRepository<UserProfile> profiles, ReportQuery reports)
{
    [McpServerTool(Name = "get_settings", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Read the user's time zone, language and week start before interpreting local dates or preparing a timesheet.")]
    public async Task<SettingsModel> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var owner = await actor.GetCurrentUserIdRequiredAsync();
        var profile = await profiles.RequireAsync(owner, owner, cancellationToken);
        return new SettingsModel { Version = profile.Version, TimeZone = profile.TimeZone, Language = profile.Language,
            FirstDayOfWeek = profile.FirstDayOfWeek, OnboardingCompleted = profile.OnboardingCompletedAt.HasValue };
    }

    [McpServerTool(Name = "list_projects", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("List the user's projects and their stable IDs. Archived projects can be read but cannot be selected for new entries.")]
    public async Task<IReadOnlyList<ProjectModel>> ListProjectsAsync(CancellationToken cancellationToken)
        => await projects.ListAsync(new ProjectsSpecification(await actor.GetCurrentUserIdRequiredAsync()), cancellationToken);

    [McpServerTool(Name = "list_tags", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Find used tag names, optionally by case-insensitive search. Tags need no separate creation: use their names on entries.")]
    public async Task<IReadOnlyList<TagModel>> ListTagsAsync(CancellationToken cancellationToken, string? search = null)
        => await tags.ListAsync(new TagsSpecification(await actor.GetCurrentUserIdRequiredAsync(), search), cancellationToken);

    [McpServerTool(Name = "list_time_entries", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("List the user's entries overlapping [from, to), newest first. Use ISO 8601 instants with Z or a UTC offset. Project can be an ID or 'none'; tag and text filters ignore case.")]
    public async Task<IReadOnlyList<TimeEntryModel>> ListTimeEntriesAsync(DateTimeOffset from, DateTimeOffset to,
        CancellationToken cancellationToken, string? project = null, string? tag = null, string? text = null)
    {
        if (to <= from) throw new AppException("InvalidInterval");
        return await entries.ListAsync(new TimeEntriesSpecification(await actor.GetCurrentUserIdRequiredAsync(), from, to, project, tag, text), cancellationToken);
    }

    [McpServerTool(Name = "get_time_entry", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Read one of the user's entries, including its current version for a subsequent update or deletion.")]
    public Task<TimeEntryModel> GetTimeEntryAsync(string id, CancellationToken cancellationToken) => ReadEntryAsync(id, cancellationToken);

    [McpServerTool(Name = "get_report", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Summarize confirmed entries in [from, to), with project totals, local-day totals in the user's time zone, and entries. Durations are clipped to the interval. Project can be an ID or 'none'.")]
    public Task<ReportModel> GetReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken,
        string? project = null, string? tag = null, string? text = null)
        => reports.RunAsync(from, to, project, tag, text, cancellationToken);

    [McpServerTool(Name = "get_timer", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Read the user's running timer, or null if none is running.")]
    public async Task<TimeEntryModel?> GetTimerAsync(CancellationToken cancellationToken)
    {
        var owner = await actor.GetCurrentUserIdRequiredAsync();
        var profile = await profiles.RequireAsync(owner, owner, cancellationToken);
        return profile.RunningEntryId is null ? null : await ReadEntryAsync(profile.RunningEntryId, cancellationToken);
    }

    [McpServerTool(Name = "create_time_entry", Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Create a confirmed, finished entry after reviewing it with the user. Supply a new nonempty UUID createRequestId and reuse it on retries to avoid duplicates. Start/end require Z or a UTC offset. Description/project are optional; zero or more tags are trimmed and deduplicated ignoring case. Overlaps are allowed.")]
    public async Task<TimeEntryModel> CreateTimeEntryAsync(Guid createRequestId, DateTimeOffset start, DateTimeOffset end,
        CancellationToken cancellationToken, string description = "", string? projectId = null, List<string>? tags = null)
    {
        if (createRequestId == Guid.Empty) throw new AppException("InvalidRequest");
        var id = await sender.Send(new SaveTimeEntryUsecase(null, new EditTimeEntry
        { CreateRequestId = createRequestId, Start = start, End = end, Description = description, ProjectId = projectId, Tags = tags ?? [] }), cancellationToken);
        return await ReadEntryAsync(id, cancellationToken);
    }

    [McpServerTool(Name = "update_time_entry", Destructive = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Replace an entry's editable fields after user review. First read its latest version; a stale version fails with Conflict. Supply all fields, retaining existing values where appropriate. Null end is allowed only for the current running timer. Times require Z or a UTC offset.")]
    public async Task<TimeEntryModel> UpdateTimeEntryAsync(string id, Guid version, DateTimeOffset start, DateTimeOffset? end,
        string description, string? projectId, List<string> tags, CancellationToken cancellationToken)
    {
        await sender.Send(new SaveTimeEntryUsecase(id, new EditTimeEntry
        { Version = version, Start = start, End = end, Description = description, ProjectId = projectId, Tags = tags }), cancellationToken);
        return await ReadEntryAsync(id, cancellationToken);
    }

    [McpServerTool(Name = "delete_time_entry", Destructive = true, OpenWorld = false)]
    [Description("Delete an entry after the user confirms the deletion. Read its latest version first; stale versions fail with Conflict. The entry is also removed from reports.")]
    public Task<string> DeleteTimeEntryAsync(string id, Guid version, CancellationToken cancellationToken)
        => sender.Send(new DeleteTimeEntryUsecase(id, version), cancellationToken);

    [McpServerTool(Name = "start_timer", Destructive = true, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Start tracking now after user review. Atomically stops any running timer and starts a new one. Repeating this call starts another timer; check get_timer after an uncertain result before retrying. Description/project and tags are optional.")]
    public async Task<TimeEntryModel> StartTimerAsync(CancellationToken cancellationToken, string description = "", string? projectId = null, List<string>? tags = null)
    {
        var id = await sender.Send(new StartTimerUsecase(new EditTimeEntry { Description = description, ProjectId = projectId, Tags = tags ?? [] }), cancellationToken);
        return await ReadEntryAsync(id, cancellationToken);
    }

    [McpServerTool(Name = "stop_timer", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Stop the specified running entry now after user review. Get its ID from get_timer. Repeating a stop for an already finished entry is safe and never stops another timer.")]
    public async Task<TimeEntryModel> StopTimerAsync(string id, CancellationToken cancellationToken)
    {
        await sender.Send(new StopTimerUsecase(id), cancellationToken);
        return await ReadEntryAsync(id, cancellationToken);
    }

    private async Task<TimeEntryModel> ReadEntryAsync(string id, CancellationToken cancellationToken)
    {
        var entry = await entries.RequireAsync(await actor.GetCurrentUserIdRequiredAsync(), id, cancellationToken);
        return new TimeEntryModel { Id = entry.Id, CompatibilityId = entry.CompatibilityId, Version = entry.Version,
            Description = entry.Description, ProjectId = entry.ProjectId, Tags = entry.Tags, Start = entry.Start, End = entry.End };
    }
}
