namespace Shared.Infrastructure.Authentication.ApiKey;

public sealed record ApiKeyInfo(string Id, string Name, string Suffix, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);
