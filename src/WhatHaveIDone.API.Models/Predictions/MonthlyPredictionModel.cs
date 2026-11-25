namespace WhatHaveIDone.API.Models;

public sealed record MonthlyPredictionModel(DateOnly Month, DateTimeOffset CalculatedAt, bool Enabled,
    double ActualSeconds, double? PredictedSeconds, double? DailySeconds);
