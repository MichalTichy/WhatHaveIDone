namespace WhatHaveIDone.API.Endpoints;

internal sealed class StartTimerRequest
{
    public string Description { get; init; } = "";
    public string? ProjectId { get; init; }
    public List<string> Tags { get; init; } = [];
}
