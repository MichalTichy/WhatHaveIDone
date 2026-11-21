# Shared.Infrastructure.Authentication.ApiKey

Locally authored capability; not copied from RSM. Root repository rules apply.

The core has no Marten dependency. It owns API-key generation (32 cryptographically random bytes), SHA-256 hashing, safe metadata, listing/revocation workflow, and ASP.NET authentication. Consumers inject IApiKeyStorageService; it must persist only the hash and metadata, query active keys globally before user tenancy exists, scope management operations by the supplied trusted owner, and participate in the host transaction or commit standalone writes. No storage provider is registered implicitly.

With ASP.NET Identity already configured, use AuthenticationBuilder.AddApiKeys<TUser>() and register a storage service. The module supplies ApiKeyManager and the default IdentityApiKeyValidator<TUser> using UserManager and IUserClaimsPrincipalFactory. Missing/locked users are rejected on every request; no validation cache. It reuses or supplies the shared HTTP current-user provider and TimeProvider. It never authenticates management operations from caller-supplied user IDs.

KeyPrefix defaults to key_; options can set a product prefix. New key secrets are returned only from CreateAsync. ListAsync returns ApiKeyInfo without the key or its hash. RevokeAsync returns false for a missing/foreign key and is idempotent for the owner. Application adapters can translate argument/not-found results into their own localized API contract. Document IDs and existing prefix/hash formats can be preserved by a storage adapter.

The handler accepts one bounded Authorization header and authenticates Bearer keys. IApiKeyValidator can be replaced for another account system. Request cancellation reaches storage calls. ReadAdditionalKey permits a protocol adapter to extract another credential format; AdditionalChallenge returns a complete WWW-Authenticate value for its paths or null for the default Bearer challenge. Keep product paths and protocol literals out of the core. Never accept keys from cookies/query strings or log/echo credentials.

The separate Shared.Infrastructure.Authentication.ApiKey.Marten project supplies Marten persistence/configuration. Application tests exercise the core with an in-memory storage service and separate HTTP host, plus real HTTP/DB adapters. Referencing the assembly activates nothing; there is no automatic installer.

HeaderName defaults to Authorization (Bearer credentials, plus optional protocol callbacks). Set HeaderName to e.g. X-Api-Key to read a raw key from that header instead. The configured header is the only credential source; duplicate/blank/oversized values fail. HeaderName is validated as an HTTP token. Keys whose case-sensitive prefix does not match KeyPrefix are rejected before invoking the validator or accessing storage. Keep KeyPrefix stable when retaining existing keys. Protocol adapters using ReadAdditionalKey apply to Authorization parsing only.
