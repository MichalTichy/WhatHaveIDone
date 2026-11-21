using System.Text.Json.Serialization;

namespace WhatHaveIDone.API.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ImportStatus>))]
public enum ImportStatus
{
    Queued,
    Running,
    Waiting,
    NeedsAttention,
    AwaitingConfirmation,
    Completed,
    Cancelled = 7
}
