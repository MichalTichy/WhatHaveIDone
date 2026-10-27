namespace WhatHaveIDone.API.Models;

public sealed record ImportModel
{
    public string Id { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Status { get; init; } = "";
    public string Stage { get; init; } = "";
    public bool Confirmed { get; init; }
    public string SourceEmail { get; init; } = "";
    public string SourceTimeZone { get; init; } = "Europe/Prague";
    public List<string> AvailableEmails { get; init; } = [];
    public List<ImportWorkspaceModel> Workspaces { get; init; } = [];
    public int TotalRows { get; init; }
    public int PreviewErrors { get; init; }
    public bool HasOriginalIds { get; init; }
    public List<ImportPreviewRow> Preview { get; init; } = [];
    public int Imported { get; init; }
    public int Skipped { get; init; }
    public int Errors { get; init; }
    public DateTimeOffset? NextAttemptAt { get; init; }
    public DateTimeOffset? RangeStart { get; init; }
    public DateTimeOffset? RangeEnd { get; init; }
    public string? MessageCode { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
