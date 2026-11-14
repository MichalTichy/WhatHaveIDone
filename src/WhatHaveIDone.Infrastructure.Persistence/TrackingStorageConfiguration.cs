using Marten;
using Marten.Schema;
using Marten.Schema.Indexing.Unique;
using Shared.Infrastructure.Persistence.Marten.StorageConfiguration;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Infrastructure.Persistence;

public sealed class TrackingStorageConfiguration : IStorageConfiguration
{
    public void Configure(StoreOptions options)
    {
        options.DatabaseSchemaName = "tracking";
        options.Schema.For<UserProfile>().MultiTenanted().UseOptimisticConcurrency(true);
        options.Schema.For<Client>().MultiTenanted().UseOptimisticConcurrency(true)
            .UniqueIndex(UniqueIndexType.Computed, "client_compatibility", TenancyScope.PerTenant, x => x.CompatibilityId);
        options.Schema.For<TimeEntry>().MultiTenanted().UseOptimisticConcurrency(true)
            .UniqueIndex(UniqueIndexType.Computed, "entry_compatibility", TenancyScope.PerTenant, x => x.CompatibilityId);
        options.Schema.For<Project>().MultiTenanted().UseOptimisticConcurrency(true)
            .UniqueIndex(UniqueIndexType.Computed, "project_compatibility", TenancyScope.PerTenant, x => x.CompatibilityId);
        options.Schema.For<Tag>().MultiTenanted().UseOptimisticConcurrency(true)
            .UniqueIndex(UniqueIndexType.Computed, "tag_name", TenancyScope.PerTenant, x => x.NormalizedName)
            .UniqueIndex(UniqueIndexType.Computed, "tag_compatibility", TenancyScope.PerTenant, x => x.CompatibilityId);
        options.Schema.For<ApiToken>().SingleTenanted().UseOptimisticConcurrency(true).Index(x => x.UserId).UniqueIndex(x => x.Hash);
    }
}
