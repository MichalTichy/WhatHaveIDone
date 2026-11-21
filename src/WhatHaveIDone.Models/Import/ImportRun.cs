using Marten.Schema;
using WhatHaveIDone.API.Models;

namespace WhatHaveIDone.Models;

public sealed class ImportRun : Document
{
    [TenantIdMetadata]
    public string TenantId { get; set; } = "";
    public List<ImportCall> PendingCalls { get; set; } = [];
    public HashSet<string> ConsumedCursors { get; set; } = [];
    public Dictionary<long, ImportedProject> SourceProjects { get; set; } = [];
    public DateOnly? RequestedFrom { get; init; }
    public DateOnly? RequestedThrough { get; init; }
    public ImportStatus Status { get; set; } = ImportStatus.Queued;
    public string SourceEmail { get; set; } = "";
    public List<ImportWorkspace> Workspaces { get; set; } = [];
    public Dictionary<long, string> TagNames { get; set; } = [];
    public string? ProtectedToken { get; set; }
    public string SourceTimeZone { get; set; } = "Europe/Prague";
    public long SourceUserId { get; set; }
    public DateTimeOffset? RangeEnd { get; set; }
    public DateTimeOffset? RangeStart { get; set; }
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? MessageCode { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsActive => Status.IsActive();
}
