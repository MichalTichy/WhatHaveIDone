using System.Security.Claims;

namespace Shared.Infrastructure.Authentication.ApiKey;

public interface IApiKeyValidator
{
    // Return null for an invalid key; claims belong to the authenticated key's subject.
    Task<IReadOnlyCollection<Claim>?> ValidateAsync(string key, CancellationToken cancellationToken);
}
