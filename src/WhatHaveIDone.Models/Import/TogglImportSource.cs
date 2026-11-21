namespace WhatHaveIDone.Models;

public sealed record TogglImportSource(long UserId, string Email, string TimeZone,
    List<ImportWorkspace> Workspaces, Dictionary<long, ImportedProject> Projects, Dictionary<long, string> Tags);
