namespace WhatHaveIDone.Models;

public sealed class UserProfile : Document
{
    public string TimeZone { get; set; } = "Europe/Prague";
    public string Language { get; set; } = "cs";
    public int FirstDayOfWeek { get; set; } = 1;
    public DateTimeOffset? OnboardingCompletedAt { get; set; }
    public string? RunningEntryId { get; set; }
    public Guid SettingsVersion { get; set; } = Guid.NewGuid();
    public ImportRun? Import { get; set; }
    public List<ImportRun> ImportHistory { get; set; } = [];
    public string? DefaultClientId { get; set; }
    public long NextCompatibilityId { get; set; } = 9_000_000_000_000;
    public long AllocateId() => NextCompatibilityId++;
}
