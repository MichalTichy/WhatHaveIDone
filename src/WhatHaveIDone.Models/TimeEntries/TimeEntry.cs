namespace WhatHaveIDone.Models;

public sealed class TimeEntry : OwnedDocument
{
    public required long CompatibilityId { get; init; }
    public string Description { get; set; } = "";
    public string? ProjectId { get; set; }
    public List<string> Tags { get; set; } = [];
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset? End { get; set; }
    public string? SourceKey { get; init; }

    public void Change(string? description, string? projectId, IEnumerable<string> tags, DateTimeOffset start, DateTimeOffset? end)
    {
        if (end.HasValue && end < start) throw new ArgumentException("InvalidInterval");
        Description = description?.Trim() ?? "";
        ProjectId = string.IsNullOrWhiteSpace(projectId) ? null : projectId;
        Tags = tags.ToList();
        Start = start.ToUniversalTime();
        End = end?.ToUniversalTime();
    }

    public double SecondsIn(DateTimeOffset from, DateTimeOffset to, DateTimeOffset now)
        => Math.Max(0, ((End ?? now) < to ? End ?? now : to).Subtract(Start > from ? Start : from).TotalSeconds);
}
