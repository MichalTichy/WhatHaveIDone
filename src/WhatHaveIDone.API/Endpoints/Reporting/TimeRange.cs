using FastEndpoints;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class TimeRange
{
    [BindFrom("from"), QueryParam(IsRequired = true)] public DateTimeOffset From { get; init; }
    [BindFrom("to"), QueryParam(IsRequired = true)] public DateTimeOffset To { get; init; }
    [QueryParam] public string? Project { get; init; }
    [QueryParam] public string? Tag { get; init; }
    [QueryParam] public string? Text { get; init; }
}
