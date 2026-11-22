using FastEndpoints;
using WhatHaveIDone.API.Models;

namespace WhatHaveIDone.API.Endpoints;

internal sealed record UpdateTimeEntryRequest : EditTimeEntry
{
    [RouteParam(IsRequired = true)] public string Id { get; init; } = "";
}
