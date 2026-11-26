namespace WhatHaveIDone.API.Models;

public sealed record DataStatistics(StatisticKind Name, double? Value, StatisticUnit Unit);
