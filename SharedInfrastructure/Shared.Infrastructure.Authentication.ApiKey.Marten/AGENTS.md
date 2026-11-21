# Shared.Infrastructure.Authentication.ApiKey.Marten

Marten implementation of the core IApiKeyStorageService contract.

## Setup

Register the existing shared Marten persistence and call services.AddMartenApiKeyStorage(). This registers IApiKeyStorageService and IStorageConfiguration; it uses the existing shared no-tenancy repositories and session factory. Configure Identity and AuthenticationBuilder.AddApiKeys<TUser>() separately. The shared persistence setup applies the registered IStorageConfiguration when constructing the store.

The no-argument overload stores the provider-neutral ApiKeyRecord. AddMartenApiKeyStorage<TRecord>() supports an existing document type deriving ApiKeyRecord with a public parameterless constructor, preserving stored types, aliases and IDs. No Marten interfaces are required on that type.

## Rules

- Only MartenApiKeyExtensions is public. Storage implementation and configuration are internal; other providers implement IApiKeyStorageService from the core library.
- Keys are SingleTenanted: authentication discovers the owner before a company/user tenant is available. Active lookup filters hash and revocation; listing filters the owner and projects safe metadata in PostgreSQL.
- Use INoTenancyReadOnlyRepository for reads with internal specifications and INoTenancyRepository for writes. No direct document sessions belong in this service. Repositories own standalone transactions or join the application's ambient unit of work. Revocation wraps its read and write in one UnitOfWorkProvider. The outer owner commits before exposing a newly created secret. Pass cancellation through repository calls and commit.
- Revocation checks the owner in the write transaction, preserves the first timestamp and uses optimistic concurrency. Hashes have a unique index.
- Keep Identity, application endpoints and protocol parsing out of this adapter. Referencing the library alone activates nothing.
