using JasperFx.Metadata;
using Shared.Infrastructure.Persistence;

namespace WhatHaveIDone.Models;

public abstract class OwnedDocument : IItemWithId, IVersioned
{
    public required string Id { get; init; }
    public required string OwnerId { get; init; }
    public Guid Version { get; set; }
}
