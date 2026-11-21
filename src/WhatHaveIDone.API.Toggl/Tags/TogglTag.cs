using System.Text.Json.Serialization;

namespace WhatHaveIDone.API.Toggl.Tags;

public sealed record TogglTag(long Id, string Name)
{
    public int Wid => 1;
    [JsonPropertyName("workspace_id")] public int WorkspaceId => 1;
}
