namespace WhatHaveIDone.Contracts;

public sealed record ProjectModel
{
    public string Id { get; set; } = "";
    public long CompatibilityId { get; set; }
    public Guid Version { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#009cde";
    public string? ClientId { get; set; }
    public bool Archived { get; set; }
}
