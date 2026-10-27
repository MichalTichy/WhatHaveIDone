namespace WhatHaveIDone.Contracts;

public sealed record SessionModel(bool Authenticated, string? Email, bool HasPassword, bool SsoEnabled, string? CsrfToken);
