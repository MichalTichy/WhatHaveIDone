namespace WhatHaveIDone.Logic.Projects;

public sealed record ProjectInterval(string ProjectId, DateTimeOffset Start, DateTimeOffset? End);
