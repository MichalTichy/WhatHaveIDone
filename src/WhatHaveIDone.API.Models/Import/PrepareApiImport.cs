namespace WhatHaveIDone.API.Models;
public sealed record PrepareApiImport(string Token, DateOnly? From = null, DateOnly? Through = null);
