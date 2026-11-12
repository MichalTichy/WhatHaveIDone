using JasperFx.Metadata;
using Shared.Infrastructure.Persistence;

namespace WhatHaveIDone.Models;

public abstract class Document : IItemWithId, IVersioned
{
    public required string Id { get; init; }
    public Guid Version { get; set; }
}
