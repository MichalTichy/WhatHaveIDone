namespace WhatHaveIDone.Models;

public sealed class WorkCalendarMonth : Document
{
    public Dictionary<int, int> Overrides { get; set; } = [];
}
