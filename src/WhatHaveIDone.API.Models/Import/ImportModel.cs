namespace WhatHaveIDone.API.Models;

public sealed record ImportModel
{
    public string Id { get; init; } = "";
    public ImportStatus Status { get; init; }
    public string SourceEmail { get; init; } = "";
    public string SourceTimeZone { get; init; } = "Europe/Prague";
    public List<ImportWorkspaceModel> Workspaces { get; init; } = [];
    public int Imported { get; init; }
    public int Skipped { get; init; }
    public int Errors { get; init; }
    public DateTimeOffset? NextAttemptAt { get; init; }
    public DateOnly? RequestedFrom { get; init; }
    public DateOnly? RequestedThrough { get; init; }
    public DateTimeOffset? RangeStart { get; init; }
    public DateTimeOffset? RangeEnd { get; init; }
    public string? MessageCode { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
