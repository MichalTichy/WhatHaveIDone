using JasperFx.Metadata;
using Shared.Infrastructure.Authentication.ApiKey;
using Shared.Infrastructure.Persistence;

namespace WhatHaveIDone.Models;

// Keep the persisted document type and alias stable while the module owns its data and behavior.
public sealed class ApiToken : ApiKeyRecord, IItemWithId, IVersioned;
