using System.Threading.RateLimiting;
using System.Net;
using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.CurrentUserProvider;
using Shared.Infrastructure.CurrentUserProvider.HttpContext;
using WhatHaveIDone.API;
using WhatHaveIDone.API.Authentication;
using WhatHaveIDone.API.Endpoints;
using WhatHaveIDone.Infrastructure.Identity;
using WhatHaveIDone.Infrastructure.Persistence;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Tags;
using WhatHaveIDone.Logic.TimeEntries;
using WhatHaveIDone.Logic.Reporting;
using WhatHaveIDone.Logic.Authentication;
using Marten;
using Shared.Infrastructure.Persistence.Marten.Migration;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("DB") ?? throw new InvalidOperationException("Configure ConnectionStrings:DB.");
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ICurrentUserIdProvider, HttpContextCurrentUserIdProvider>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMediatR(o => o.RegisterServicesFromAssemblyContaining<SaveTimeEntryUsecase>());
builder.Services.AddScoped<TagCatalog>();
builder.Services.AddScoped<AccountProfileProvisioner>();
builder.Services.AddScoped<ReportQuery>();
builder.Services.AddTrackingPersistence(connection);
builder.Services.AddTrackingIdentity(builder.Configuration, connection);
builder.Services.AddAuthentication(o => { o.DefaultScheme = "Combined"; o.DefaultAuthenticateScheme = "Combined"; o.DefaultChallengeScheme = "Combined"; o.DefaultForbidScheme = "Combined"; })
    .AddPolicyScheme("Combined", null, o => o.ForwardDefaultSelector = http => http.Request.Headers.Authorization.Count > 0 ? ApiTokenHandler.SchemeName : IdentityConstants.ApplicationScheme)
    .AddScheme<AuthenticationSchemeOptions, ApiTokenHandler>(ApiTokenHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(o => { o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.Name = "whid.csrf"; o.Cookie.HttpOnly = true; });
var keyPath = builder.Configuration["DataProtection:Path"] ?? Path.Combine(builder.Environment.ContentRootPath, "data", "keys");
Directory.CreateDirectory(keyPath);
builder.Services.AddDataProtection().SetApplicationName("WhatHaveIDone").PersistKeysToFileSystem(new DirectoryInfo(keyPath));
builder.Services.AddRequestLocalization(o => o.SetDefaultCulture("cs").AddSupportedCultures("en", "cs").AddSupportedUICultures("en", "cs"));
var knownProxies = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
if (knownProxies.Length > 0) builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear(); options.KnownProxies.Clear();
    foreach (var address in knownProxies) options.KnownProxies.Add(IPAddress.Parse(address));
});
builder.Services.AddRateLimiter(o => { o.RejectionStatusCode=429; o.AddPolicy("accounts", http => RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
    _ => new FixedWindowRateLimiterOptions { PermitLimit = Math.Max(1,builder.Configuration.GetValue<int?>("Accounts:PermitLimit")??12), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })); });

var app = builder.Build();
if (knownProxies.Length > 0) app.UseForwardedHeaders();
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseRequestLocalization();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (http, next) =>
{
    if (http.Request.Path.StartsWithSegments("/api")) http.Response.Headers.CacheControl = "no-store";
    if (http.Request.Path.StartsWithSegments("/api") && http.Request.Method is "POST" or "PUT" or "DELETE" or "PATCH" && http.User.Identity?.AuthenticationType != ApiTokenHandler.SchemeName)
    {
        try { await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http); }
        catch (AntiforgeryValidationException) { throw new AppException("Csrf"); }
    }
    await next(http);
});
app.MapAccountEndpoints();
app.MapTrackingEndpoints();
app.MapReportEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.Map("/api/{**path}", () => Results.NotFound()).AllowAnonymous();
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    var ct = app.Lifetime.ApplicationStopping;
    await scope.ServiceProvider.GetRequiredService<IDocumentStore>().Storage.ApplyAllConfiguredChangesToDatabaseAsync();
    var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
    await migrator.EnsureMigrationHistoryTableAsync(ct);
    await migrator.RunMigrationsAsync(ct);
}
await app.RunAsync();

public partial class Program;
