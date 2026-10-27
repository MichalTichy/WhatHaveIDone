namespace WhatHaveIDone.Contracts;
public sealed record ConfirmImportInput(List<long> WorkspaceIds, string SourceEmail);
