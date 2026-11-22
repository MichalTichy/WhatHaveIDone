using FastEndpoints;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class TagSearch
{
    [QueryParam] public string? Search { get; init; }
}
