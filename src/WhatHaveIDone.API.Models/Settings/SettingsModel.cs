namespace WhatHaveIDone.API.Models;

public sealed record SettingsModel
{
    public Guid Version { get; set; }
    public string TimeZone { get; set; } = "Europe/Prague";
    public string Language { get; set; } = "cs";
    public int FirstDayOfWeek { get; set; } = 1;
    public bool OnboardingCompleted { get; init; }
}
