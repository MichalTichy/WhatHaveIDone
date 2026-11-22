using FastEndpoints;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class ResourceId
{
    [RouteParam(IsRequired = true)] public string Id { get; init; } = "";
}
