namespace WhatHaveIDone.Models;

public sealed record ImportCall(long WorkspaceId, DateOnly From, DateOnly Through,
    long? FirstId = null, int? FirstRow = null)
{
    public static ImportCall CreateWindow(long workspaceId, DateOnly from, DateOnly through)
        => new(workspaceId, DateOnly.FromDayNumber(Math.Max(from.DayNumber, through.DayNumber - 364)), through);

    public string CursorKey() => FormattableString.Invariant($"{WorkspaceId}:{FirstId}:{FirstRow}");
}
