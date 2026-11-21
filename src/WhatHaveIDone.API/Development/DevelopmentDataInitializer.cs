#if DEBUG
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Shared.Infrastructure.Initializer;
using Shared.Infrastructure.Persistence.Marten.UnitOfWorks;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Infrastructure.Identity;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Development;

internal sealed class DevelopmentDataInitializer(
    UserManager<AppUser> users,
    IRepository<UserProfile> profiles,
    IRepository<Client> clients,
    IRepository<Project> projects,
    IRepository<Tag> tags,
    IRepository<TimeEntry> entries,
    IWebHostEnvironment environment,
    IHostApplicationLifetime lifetime,
    TimeProvider clock) : InitializerBase
{
    private const string UserId = "development-demo";
    private const string Email = "demo@example.test";

    public override int Priority => 0;
    public override InitializerTrigger Trigger => InitializerTrigger.OnStartup;
    public override bool RunOnlyInLeaderInstance => false;

    protected override async Task RunInitializationLogicAsync()
    {
        if (!environment.IsDevelopment()) return;
        var cancellationToken = lifetime.ApplicationStopping;

        if (await users.FindByIdAsync(UserId) is null)
        {
            // An account already using the demo address belongs to its original owner.
            if (await users.FindByEmailAsync(Email) is not null) return;

            var passwordPath = Path.Combine(environment.ContentRootPath, "data", "demo-password.txt");
            if (!File.Exists(passwordPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(passwordPath)!);
                var password = "Demo!Aa1" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
                await File.WriteAllTextAsync(passwordPath, password, cancellationToken);
            }

            var passwordFromFile = await File.ReadAllTextAsync(passwordPath, cancellationToken);
            var result = await users.CreateAsync(
                new AppUser { Id = UserId, Email = Email, UserName = Email, Name = "Demo" },
                passwordFromFile.Trim());
            if (!result.Succeeded)
                throw new InvalidOperationException("Could not create the development demo account: "
                    + string.Join(", ", result.Errors.Select(error => error.Code)));
        }

        await using var transaction = new UnitOfWorkProvider();
        if (await profiles.GetByIdAsync(UserId, cancellationToken, tenantId: UserId) is not null) return;

        var now = clock.GetUtcNow();
        var profile = new UserProfile { Id = UserId, OnboardingCompletedAt = now };
        var client = Client.CreateDefault(UserId, profile.AllocateId());
        profile.DefaultClientId = client.Id;
        await clients.AddAsync(client, cancellationToken, tenantId: UserId);

        var app = new Project { Id = "demo-app", CompatibilityId = profile.AllocateId(), Name = "App development", ClientId = client.Id };
        var operations = new Project { Id = "demo-operations", CompatibilityId = profile.AllocateId(), Name = "Operations", Color = "#667fb7", ClientId = client.Id };
        await projects.AddAsync(app, cancellationToken, tenantId: UserId);
        await projects.AddAsync(operations, cancellationToken, tenantId: UserId);

        var zone = TimeZoneInfo.FindSystemTimeZoneById(profile.TimeZone);
        var today = TimeZoneInfo.ConvertTime(now, zone).Date;
        var samples = new[]
        {
            (Id: "demo-planning", Description: "Plan the next release", ProjectId: (string?)app.Id, Tag: (string?)"planning", Start: today.AddHours(9), End: today.AddHours(10.5)),
            (Id: "demo-sync", Description: "Team sync", ProjectId: (string?)operations.Id, Tag: (string?)"meetings", Start: today.AddHours(10), End: today.AddHours(11)),
            (Id: "demo-unassigned", Description: "", ProjectId: (string?)null, Tag: (string?)null, Start: today.AddHours(13), End: today.AddHours(13.75)),
            (Id: "demo-overnight", Description: "Check overnight deployment", ProjectId: (string?)operations.Id, Tag: (string?)"deployment", Start: today.AddHours(-1), End: today.AddHours(1))
        };
        foreach (var sample in samples)
        {
            var entry = new TimeEntry { Id = sample.Id, CompatibilityId = profile.AllocateId() };
            if (sample.Tag is { } tag)
            {
                await tags.AddAsync(new Tag
                {
                    Id = "demo-tag-" + tag, CompatibilityId = profile.AllocateId(), Name = tag, NormalizedName = Tag.Normalize(tag)
                }, cancellationToken, tenantId: UserId);
            }
            entry.Change(sample.Description, sample.ProjectId, sample.Tag is { } name ? [name] : [],
                TimeZoneInfo.ConvertTimeToUtc(sample.Start, zone), TimeZoneInfo.ConvertTimeToUtc(sample.End, zone));
            await entries.AddAsync(entry, cancellationToken, tenantId: UserId);
        }

        await profiles.AddAsync(profile, cancellationToken, tenantId: UserId);
        await transaction.CommitAsync(cancellationToken);
    }
}
#endif
