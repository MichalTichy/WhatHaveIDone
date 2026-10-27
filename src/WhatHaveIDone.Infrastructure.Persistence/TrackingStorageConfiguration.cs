using Marten;
using Marten.Schema;
using Shared.Infrastructure.Persistence.Marten.StorageConfiguration;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Infrastructure.Persistence;

public sealed class TrackingStorageConfiguration : IStorageConfiguration
{
    public void Configure(StoreOptions options)
    {
        options.DatabaseSchemaName = "tracking";
        options.Schema.For<UserProfile>().SingleTenanted().UseOptimisticConcurrency(true);
        options.Schema.For<Client>().SingleTenanted().UseOptimisticConcurrency(true).Index(x => x.OwnerId)
            .UniqueIndex(UniqueIndexType.Computed, x => x.OwnerId, x => x.CompatibilityId);
        options.Schema.For<TimeEntry>().SingleTenanted().UseOptimisticConcurrency(true).Index(x => x.OwnerId)
            .UniqueIndex(UniqueIndexType.Computed, x => x.OwnerId, x => x.CompatibilityId);
        options.Schema.For<Project>().SingleTenanted().UseOptimisticConcurrency(true).Index(x => x.OwnerId)
            .UniqueIndex(UniqueIndexType.Computed, x => x.OwnerId, x => x.CompatibilityId);
        options.Schema.For<Tag>().SingleTenanted().UseOptimisticConcurrency(true).Index(x => x.OwnerId)
            .UniqueIndex(UniqueIndexType.Computed, x => x.OwnerId, x => x.NormalizedName)
            .UniqueIndex(UniqueIndexType.Computed, x => x.OwnerId, x => x.CompatibilityId);
        options.Schema.For<ApiToken>().SingleTenanted().UseOptimisticConcurrency(true).Index(x => x.OwnerId).UniqueIndex(x => x.Hash);
        options.Schema.For<ImportRun>().SingleTenanted().UseOptimisticConcurrency(true).Index(x => x.OwnerId).Index(x => x.Status);
        options.Schema.For<ImportExclusion>().SingleTenanted().UseOptimisticConcurrency(true).Index(x => x.OwnerId);
    }
}
