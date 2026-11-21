namespace WhatHaveIDone.API.Models;

public static class ImportStatusExtensions
{
    public static bool CanAdvance(this ImportStatus status)
        => status is ImportStatus.Queued or ImportStatus.Running or ImportStatus.Waiting;

    public static bool IsActive(this ImportStatus status)
        => status.CanAdvance() || status is ImportStatus.NeedsAttention or ImportStatus.AwaitingConfirmation;
}
