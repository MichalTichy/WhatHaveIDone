namespace WhatHaveIDone.Models;

public sealed class ApiToken : OwnedDocument
{
    public required string Name { get; init; }
    public required string Hash { get; init; }
    public required string Suffix { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? RevokedAt { get; set; }
}
