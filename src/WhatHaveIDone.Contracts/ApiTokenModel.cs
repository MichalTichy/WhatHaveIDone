namespace WhatHaveIDone.Contracts;

public sealed record ApiTokenModel(string Id, string Name, string Suffix, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);
