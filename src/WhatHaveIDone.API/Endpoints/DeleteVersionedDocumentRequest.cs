using FastEndpoints;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class DeleteVersionedDocumentRequest
{
    [RouteParam(IsRequired = true)] public string Id { get; init; } = "";
    [QueryParam(IsRequired = true)] public Guid Version { get; init; }
}
