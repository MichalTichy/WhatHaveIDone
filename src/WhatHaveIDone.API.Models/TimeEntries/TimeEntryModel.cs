namespace WhatHaveIDone.API.Models;

public sealed record TimeEntryModel
{
    public string Id { get; init; } = "";
    public long CompatibilityId { get; init; }
    public Guid Version { get; init; }
    public string Description { get; init; } = "";
    public string? ProjectId { get; init; }
    public List<string> Tags { get; init; } = [];
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset? End { get; init; }
}
