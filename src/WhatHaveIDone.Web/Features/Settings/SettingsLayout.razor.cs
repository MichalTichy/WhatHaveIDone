using Microsoft.AspNetCore.Components;
using WhatHaveIDone.Web.Services;

namespace WhatHaveIDone.Web.Features.Settings;

public partial class SettingsLayout
{
    [Inject]
    private UserContext User { get; set; } = null!;
}
