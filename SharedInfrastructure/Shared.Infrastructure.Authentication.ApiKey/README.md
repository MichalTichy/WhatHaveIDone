# API keys

ASP.NET Core authentication and API-key management backed by an injected `IApiKeyStorageService`. The core does not depend on Marten. ASP.NET Identity must already be configured for the default validator.

```csharp
// Use the separate Marten adapter with the application's existing shared Marten setup:
services.AddMartenApiKeyStorage();
services.AddAuthentication(ApiKeyAuthenticationDefaults.AuthenticationScheme)
    .AddApiKeys<MyUser>(options => options.KeyPrefix = "myapp_");
```

The Marten adapter registers its storage configuration and storage service; it does not create a second document store. For another backend, register your own scoped `IApiKeyStorageService` instead of calling `AddMartenApiKeyStorage`. Its contract includes active hash lookup, owner-filtered safe metadata, transactional inserts and owner-checked idempotent revocation.

Inject `ApiKeyManager` into authenticated operations. `CreateAsync(name, ct)` generates a 256-bit random key and returns its secret once; the host must complete any ambient transaction before exposing it. `ListAsync(ct)` returns metadata without hashes or secrets. `RevokeAsync(id, ct)` returns false for absent/foreign keys. Management gets the owner from the shared current-user provider; clients cannot choose it. `TimeProvider` and the HTTP provider are supplied only if not already registered.

Authentication rejects malformed/multiple/oversized headers, looks up the SHA-256 hash on every request and checks the Identity account through `UserManager`. Claims come from `IUserClaimsPrincipalFactory`. Revoked keys and missing/locked accounts fail immediately; no authentication cache. Browser-cookie selection, CSRF protection, endpoint authorization and localized HTTP contracts remain with the host.

`IApiKeyValidator` can be replaced for another account system. Optional `ReadAdditionalKey` and `AdditionalChallenge` callbacks let protocol adapters add a credential format without putting protocol literals or product paths in the core. Bearer is the default. Do not read keys from cookies or query strings.

Applications retaining an existing Marten document can derive it from `ApiKeyRecord` and use `AddMartenApiKeyStorage<TDocument>()`. This preserves their stored type/alias, IDs and hash format. The no-argument overload stores `ApiKeyRecord` directly. Existing document-store configuration must register the adapter's `IStorageConfiguration` before constructing the store.

The module implements revocable personal API keys, without expiry, refresh grants or scopes.

HeaderName defaults to Authorization (Bearer credentials, plus optional protocol callbacks). Set HeaderName to e.g. X-Api-Key to read a raw key from that header instead. The configured header is the only credential source; duplicate/blank/oversized values fail. HeaderName is validated as an HTTP token. Keys whose case-sensitive prefix does not match KeyPrefix are rejected before invoking the validator or accessing storage. Keep KeyPrefix stable when retaining existing keys. Protocol adapters using ReadAdditionalKey apply to Authorization parsing only.
