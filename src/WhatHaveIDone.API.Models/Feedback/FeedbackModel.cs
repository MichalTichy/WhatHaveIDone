namespace WhatHaveIDone.API.Models;

public sealed record FeedbackModel(string Id, string Text, DateTimeOffset CreatedAt);
