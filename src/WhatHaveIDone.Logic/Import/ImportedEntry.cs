namespace WhatHaveIDone.Logic.Import;
public sealed record ImportedEntry(long OriginalId, string Description, long? ProjectId, string? ProjectName, List<string> Tags,
    DateTimeOffset Start, DateTimeOffset? End, long SourceUserId, string? ErrorCode = null);
