namespace WhatHaveIDone.API.Models;

public sealed record StatisticsComparison(DateOnly From, DateOnly Through, IReadOnlyList<DataStatistics> Statistics);
