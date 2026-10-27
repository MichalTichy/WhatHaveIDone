namespace WhatHaveIDone.Models;

public sealed class Tag : OwnedDocument
{
    public required long CompatibilityId { get; init; }
    public required string Name { get; init; }
    public required string NormalizedName { get; init; }
    public static string Normalize(string name) => name.Trim().Normalize().ToUpperInvariant();
}
