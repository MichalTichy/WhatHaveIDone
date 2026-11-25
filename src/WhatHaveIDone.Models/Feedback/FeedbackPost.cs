namespace WhatHaveIDone.Models;

// Deliberately global: feedback is shared by all signed-in users.
public sealed class FeedbackPost : Document
{
    public required string Text { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
