namespace WhatHaveIDone.Models;

public sealed record ImportCall(long WorkspaceId, DateOnly From, DateOnly Through,
    long? FirstId = null, int? FirstRow = null)
{
    public string CursorKey() => FormattableString.Invariant($"{WorkspaceId}:{FirstId}:{FirstRow}");
}
