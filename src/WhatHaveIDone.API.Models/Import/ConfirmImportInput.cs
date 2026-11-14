namespace WhatHaveIDone.API.Models;
public sealed record ConfirmImportInput(List<long> WorkspaceIds, string SourceEmail);
