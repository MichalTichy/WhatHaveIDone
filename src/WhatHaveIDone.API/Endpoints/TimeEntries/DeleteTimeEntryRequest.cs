using FastEndpoints;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class DeleteTimeEntryRequest
{
    [RouteParam(IsRequired = true)] public string Id { get; init; } = "";
    [QueryParam(IsRequired = true)] public Guid Version { get; init; }
}
