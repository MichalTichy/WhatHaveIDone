namespace WhatHaveIDone.API.Models;

public sealed record ApiTokenModel(string Id, string Name, string Suffix, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);
