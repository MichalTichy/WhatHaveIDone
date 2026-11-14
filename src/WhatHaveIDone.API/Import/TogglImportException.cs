namespace WhatHaveIDone.API.Import;

public sealed class TogglImportException(string code,DateTimeOffset? nextAttempt=null) : Exception(code)
{
    public string Code { get; } = code;
    public DateTimeOffset? NextAttempt { get; } = nextAttempt;
}
