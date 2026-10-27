namespace WhatHaveIDone.Models;

public sealed class ImportExclusion : OwnedDocument
{
    public required string SourceKey { get; init; }
}
