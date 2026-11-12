namespace WhatHaveIDone.API.Models;

public sealed record ChangePasswordInput(string CurrentPassword, string NewPassword);
