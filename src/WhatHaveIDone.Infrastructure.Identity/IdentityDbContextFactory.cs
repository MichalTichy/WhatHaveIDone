using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WhatHaveIDone.Infrastructure.Identity;

public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DB") ?? throw new InvalidOperationException("Set ConnectionStrings__DB for migrations.");
        return new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connection, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "identity")).Options);
    }
}
