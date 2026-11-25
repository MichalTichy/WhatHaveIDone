namespace WhatHaveIDone.API.Models;

public sealed record SaveWorkdayRequest(DateOnly Date, int? Percent, Guid Version);
