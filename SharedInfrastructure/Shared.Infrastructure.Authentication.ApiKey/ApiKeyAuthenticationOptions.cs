using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Shared.Infrastructure.Authentication.ApiKey;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    [Required, RegularExpression("[A-Za-z0-9_-]{1,64}")]
    public string KeyPrefix { get; set; } = "key_";
    [Required, RegularExpression("[!#$%&'*+.^_`|~0-9A-Za-z-]{1,128}")]
    public string HeaderName { get; set; } = "Authorization";
    public Func<HttpRequest, string?>? ReadAdditionalKey { get; set; }
    public Func<HttpRequest, string?>? AdditionalChallenge { get; set; }
}
