namespace WhatHaveIDone.API.Models;

public sealed record WorkdayModel(DateOnly Date, int Percent, int DefaultPercent, bool IsOverride, string? Holiday);
