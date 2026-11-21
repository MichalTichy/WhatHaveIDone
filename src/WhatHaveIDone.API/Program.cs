using Shared.Infrastructure.Authentication.ApiKey.Marten;
using Shared.Infrastructure.Authentication.ApiKey;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
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
using WhatHaveIDone.API.Endpoints;
using WhatHaveIDone.API.Toggl;
using WhatHaveIDone.API.Import;
using WhatHaveIDone.API.Mcp;
using WhatHaveIDone.Infrastructure.Identity;
using WhatHaveIDone.Infrastructure.Persistence;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Tags;
using WhatHaveIDone.Logic.TimeEntries;
using WhatHaveIDone.Logic.Reporting;
using Marten;

var builder = WebApplication.CreateBuilder(args);
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Logging.AddOpenTelemetry(options => { options.IncludeFormattedMessage = true; options.IncludeScopes = true; });
    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation())
        .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation())
        .UseOtlpExporter();
}
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ICurrentUserIdProvider, HttpContextCurrentUserIdProvider>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMediatR(o => o.RegisterServicesFromAssemblyContaining<SaveTimeEntryUsecase>());
builder.Services.AddScoped<TagCatalog>();
builder.Services.AddScoped<ReportQuery>();
builder.Services.AddSingleton<ImportStorage>();
builder.Services.AddScoped<TogglCsvReader>();
builder.Services.AddHttpClient<TogglImportClient>(http=>{http.BaseAddress=new Uri("https://api.track.toggl.com/");http.Timeout=TimeSpan.FromSeconds(60);});
if(builder.Configuration.GetValue<bool?>("Import:WorkerEnabled")!=false)builder.Services.AddHostedService<ImportWorker>();
new PersistenceInstaller().Install(builder.Services, builder.Configuration, builder.Environment.IsProduction());
new IdentityInstaller().Install(builder.Services, builder.Configuration, builder.Environment.IsProduction());
builder.Services.AddMartenApiKeyStorage<WhatHaveIDone.Models.ApiToken>();
builder.Services.AddAuthentication(o => { o.DefaultScheme = "Combined"; o.DefaultAuthenticateScheme = "Combined"; o.DefaultChallengeScheme = "Combined"; o.DefaultForbidScheme = "Combined"; })
    .AddPolicyScheme("Combined", null, o => o.ForwardDefaultSelector = http => http.Request.Headers.Authorization.Count > 0 ? ApiKeyAuthenticationDefaults.AuthenticationScheme : IdentityConstants.ApplicationScheme)
    .AddApiKeys<AppUser>(options => { options.KeyPrefix = "whid_"; options.UseTogglCompatibility(); });
builder.Services.AddAuthorization();
builder.Services.AddTrackingMcp();
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
app.UseMiddleware<TogglApiExceptionMiddleware>();
app.UseRequestLocalization();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();
app.Use(async (http, next) =>
{
    if (http.Request.Path.StartsWithSegments("/mcp"))
    {
        http.Response.Headers.CacheControl = "no-store";
        var origin = http.Request.Headers.Origin;
        if (origin.Count > 0 && (origin.Count != 1 || !string.Equals(origin[0], $"{http.Request.Scheme}://{http.Request.Host}", StringComparison.OrdinalIgnoreCase)))
        { http.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
    }
    await next(http);
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (http, next) =>
{
    if (http.Request.Path.StartsWithSegments("/api")) http.Response.Headers.CacheControl = "no-store";
    if (http.Request.Path.StartsWithSegments("/api") && http.Request.Method is "POST" or "PUT" or "DELETE" or "PATCH" && http.User.Identity?.AuthenticationType != ApiKeyAuthenticationDefaults.AuthenticationScheme)
    {
        try { await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http); }
        catch (AntiforgeryValidationException) { throw new AppException("Csrf"); }
    }
    await next(http);
});
app.MapAccountEndpoints();
app.MapTrackingEndpoints();
app.MapReportEndpoints();
app.MapTogglEndpoints();
app.MapImportEndpoints();
app.MapTrackingMcp();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.Map("/api/{**path}", () => Results.NotFound()).AllowAnonymous();
app.MapFallbackToFile("index.html");
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<IDocumentStore>().Storage.ApplyAllConfiguredChangesToDatabaseAsync();
}
await app.RunAsync();

public partial class Program;
