namespace WhatHaveIDone.Logic.Import;
public sealed record ImportedEntry(string SourceKey, long? OriginalId, string Description, long? ProjectId, string? ProjectName, List<string> Tags,
    DateTimeOffset Start, DateTimeOffset? End, string SourceEmail = "", long SourceUserId = 0, string? ErrorCode = null);
