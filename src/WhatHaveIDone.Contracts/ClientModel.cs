namespace WhatHaveIDone.Contracts;

public sealed record ClientModel
{
    public string Id { get; set; } = "";
    public long CompatibilityId { get; set; }
    public Guid Version { get; set; }
    public string Name { get; set; } = "";
    public bool IsDefault { get; set; }
}
