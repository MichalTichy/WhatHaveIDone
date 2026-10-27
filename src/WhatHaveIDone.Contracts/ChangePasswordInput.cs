namespace WhatHaveIDone.Contracts;

public sealed record ChangePasswordInput(string CurrentPassword, string NewPassword);
