namespace WhatHaveIDone.API.Models;

public sealed record EditTimeEntry
{
    public Guid CreateRequestId { get; set; }
    public Guid Version { get; set; }
    public string Description { get; set; } = "";
    public string? ProjectId { get; set; }
    public List<string> Tags { get; set; } = [];
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset? End { get; set; }
}
