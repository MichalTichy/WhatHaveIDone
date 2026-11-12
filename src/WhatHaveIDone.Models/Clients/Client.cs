namespace WhatHaveIDone.Models;

public sealed class Client : Document
{
    public required long CompatibilityId { get; init; }
    public required string Name { get; set; }
    public bool IsDefault { get; init; }

    public static Client CreateDefault(string owner, long compatibilityId) => new()
    {
        Id = owner + ":client:default", CompatibilityId = compatibilityId,
        Name = "Personal", IsDefault = true
    };
}
