namespace Shared.Infrastructure.Authentication.ApiKey;

// Creation result carrying the secret once; the key remains valid until revoked.
public sealed record TemporaryApiKey(string Id, string Key);
