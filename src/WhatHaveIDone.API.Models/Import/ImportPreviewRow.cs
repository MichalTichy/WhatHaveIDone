namespace WhatHaveIDone.API.Models;
public sealed record ImportPreviewRow(string Email,string Description,string Project,DateTimeOffset Start,DateTimeOffset? End);
