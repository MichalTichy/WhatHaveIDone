namespace WhatHaveIDone.Models;

public sealed class Client : OwnedDocument
{
    public required long CompatibilityId { get; init; }
    public required string Name { get; set; }
    public bool IsDefault { get; init; }

    public static Client CreateDefault(string owner, long compatibilityId) => new()
    {
        Id = owner + ":client:default", OwnerId = owner, CompatibilityId = compatibilityId,
        Name = "Personal", IsDefault = true
    };
}
