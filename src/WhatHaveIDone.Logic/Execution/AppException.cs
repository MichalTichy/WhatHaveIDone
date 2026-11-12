using Shared.Infrastructure.UIException;
using WhatHaveIDone.Logic.Resources;

namespace WhatHaveIDone.Logic.Execution;

public sealed class AppException(string code, int status = 400) : UiException(Strings.ResourceManager.GetString(code) ?? code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
