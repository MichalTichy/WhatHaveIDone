namespace WhatHaveIDone.Web.Services.Api;

public sealed class ApiCallException(string code, string message, int status) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
