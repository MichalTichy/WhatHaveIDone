using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithImageTag("17.6")
    .WithDataVolume("whid-aspire-postgres");
var database = postgres.AddDatabase("db", "what_have_i_done");

builder.AddDotnetProject("api", "../WhatHaveIDone.API/WhatHaveIDone.API.csproj")
    .WithHttpEndpoint(port: 5088, name: "http")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithReference(database, connectionName: "DB")
    .WaitFor(database);

builder.Build().Run();
