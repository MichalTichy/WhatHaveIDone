namespace WhatHaveIDone.Api.Tests;
[CollectionDefinition("postgres",DisableParallelization=true)]
public sealed class PostgresCollection : ICollectionFixture<IntegrationFixture>;
