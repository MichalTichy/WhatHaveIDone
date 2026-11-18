using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Persistence.EF.Identity;

namespace WhatHaveIDone.Infrastructure.Identity;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : AspNetIdentityDbContextBase<AppUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); builder.HasDefaultSchema("identity");
        builder.Entity<AppUser>().HasIndex(x => x.NormalizedEmail).IsUnique();
        builder.Entity<AppUser>().Property(x => x.Email).IsRequired();
    }
}
