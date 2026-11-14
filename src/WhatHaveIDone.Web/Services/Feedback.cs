namespace WhatHaveIDone.Web.Services;

public sealed class Feedback
{
    public string? Message { get; private set; }
    public event Action? Changed;
    public void Show(string message) { Message = message; Changed?.Invoke(); }
    public void Clear() { Message = null; Changed?.Invoke(); }
}
