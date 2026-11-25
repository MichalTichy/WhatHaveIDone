namespace WhatHaveIDone.API.Models;

public sealed record WorkCalendarModel(int Year, int Month, Guid Version, IReadOnlyList<WorkdayModel> Days);
