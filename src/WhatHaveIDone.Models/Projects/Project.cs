namespace WhatHaveIDone.Models;

public sealed class Project : OwnedDocument
{
    public required long CompatibilityId { get; init; }
    public required string Name { get; set; }
    public string Color { get; set; } = "#009cde";
    public string? ClientId { get; set; }
    public bool Archived { get; set; }
    public string? SourceKey { get; init; }
}
