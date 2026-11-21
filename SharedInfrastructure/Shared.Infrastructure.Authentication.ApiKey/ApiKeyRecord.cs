using Shared.Infrastructure.Persistence;

namespace Shared.Infrastructure.Authentication.ApiKey;

public class ApiKeyRecord : IItemWithId
{
    public string Id { get; init; } = "";
    public Guid Version { get; set; }
    public string UserId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Hash { get; init; } = "";
    public string Suffix { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? RevokedAt { get; set; }
}
