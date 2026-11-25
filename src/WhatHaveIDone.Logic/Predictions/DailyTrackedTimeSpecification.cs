namespace WhatHaveIDone.Logic.Predictions;

public sealed record DailyTrackedTimeSpecification(DateOnly From, DateOnly Through, TimeZoneInfo TimeZone, DateTimeOffset Now);
