using Microsoft.AspNetCore.Authentication;

namespace Shared.Infrastructure.Authentication.Entra;

public sealed class DevelopmentAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SectionName = "Authentication:DevelopmentBypass";

    public string UserId { get; set; } = string.Empty;
    public string EntraId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];
}
