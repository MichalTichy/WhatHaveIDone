namespace WhatHaveIDone.Models;

public sealed record ImportCall(long WorkspaceId, DateOnly From, DateOnly Through,
    long? FirstId = null, int? FirstRow = null, ImportCallStatus Status = ImportCallStatus.Todo);
