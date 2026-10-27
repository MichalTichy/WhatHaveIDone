namespace WhatHaveIDone.Models;

public sealed class ImportRun : OwnedDocument
{
    public required string Kind { get; init; }
    public string Status { get; set; } = "Queued";
    public string Stage { get; set; } = "Profile";
    public bool Confirmed { get; set; }
    public string SourceEmail { get; set; } = "";
    public List<string> AvailableEmails { get; set; } = [];
    public List<ImportWorkspace> Workspaces { get; set; } = [];
    public Dictionary<long, string> TagNames { get; set; } = [];
    public long FileOffset { get; set; }
    public int TotalRows { get; set; }
    public int PreviewErrors { get; set; }
    public bool HasOriginalIds { get; set; }
    public List<ImportPreviewItem> Preview { get; set; } = [];
    public string? ResumeStage { get; set; }
    public string? ProtectedToken { get; set; }
    public string? FilePath { get; set; }
    public string? FileHash { get; init; }
    public string SourceTimeZone { get; set; } = "Europe/Prague";
    public long SourceUserId { get; set; }
    public List<long> WorkspaceIds { get; set; } = [];
    public int WorkspaceIndex { get; set; }
    public int Page { get; set; } = 1;
    public int? NextRow { get; set; }
    public long? NextId { get; set; }
    public DateTimeOffset? RangeEnd { get; set; }
    public DateTimeOffset? RangeStart { get; set; }
    public int FileRow { get; set; }
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? MessageCode { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsActive => Status is "Queued" or "Running" or "Waiting" or "NeedsAttention" or "AwaitingConfirmation";
}
