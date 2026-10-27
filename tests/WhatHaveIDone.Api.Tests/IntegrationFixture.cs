using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WhatHaveIDone.API.Import;
using WhatHaveIDone.Contracts;

namespace WhatHaveIDone.Api.Tests;

public sealed class IntegrationFixture : IAsyncLifetime
{
    private readonly string database="whid_tests_"+Guid.NewGuid().ToString("N");
    private string connection="";
    private bool createdDatabase;
    private readonly Dictionary<string,string?> previous=[];
    public WebApplicationFactory<Program> App { get; private set; }=null!;
    public MutableTimeProvider Clock { get; }=new();
    public FakeTogglHandler Toggl { get; }=new();
    public async Task InitializeAsync()
    {
        var root=FindRoot();var env=File.Exists(Path.Combine(root,".env"))?File.ReadAllLines(Path.Combine(root,".env")).Where(l=>l.Contains('=')).Select(l=>l.Split('=',2)).ToDictionary(p=>p[0],p=>p[1]):[];
        var supplied=Environment.GetEnvironmentVariable("WHID_TEST_CONNECTION")??$"Host=localhost;Port={env.GetValueOrDefault("POSTGRES_PORT","55432")};Database=postgres;Username=what_have_i_done;Password={env.GetValueOrDefault("POSTGRES_PASSWORD","")}";
        var builder=new NpgsqlConnectionStringBuilder(supplied){Database="postgres"};connection=builder.ConnectionString;
        await using var admin=new NpgsqlConnection(connection);await admin.OpenAsync();await using(var command=new NpgsqlCommand("CREATE DATABASE "+database,admin))await command.ExecuteNonQueryAsync();createdDatabase=true;
        builder.Database=database;
        Set("ConnectionStrings__DB",builder.ConnectionString);Set("Import__WorkerEnabled","false");Set("Accounts__PermitLimit","10000");
        var output=Path.Combine(root,"artifacts","integration",database);Directory.CreateDirectory(output);Set("DataProtection__Path",Path.Combine(output,"keys"));Set("Import__Path",Path.Combine(output,"imports"));
        App=new WebApplicationFactory<Program>().WithWebHostBuilder(web=>web.UseEnvironment("Testing").ConfigureTestServices(services=>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.AddHttpClient<TogglImportClient>().ConfigurePrimaryHttpMessageHandler(()=>Toggl).SetHandlerLifetime(Timeout.InfiniteTimeSpan);
            services.AddSingleton<ImportWorker>();
        }));
        using var client=App.CreateClient();(await client.GetAsync("/health")).EnsureSuccessStatusCode();
    }
    public async Task<OwnerClient> CreateOwnerAsync()
    {
        var http=App.CreateClient(new WebApplicationFactoryClientOptions{HandleCookies=true});var owner=new OwnerClient(http,"");await owner.RefreshCsrfAsync();
        var id=await owner.PostAsync<string>("account/register",new AccountInput{Email=Guid.NewGuid().ToString("N")+"@example.test",Password="aA1!"+Convert.ToHexString(RandomNumberGenerator.GetBytes(20))});
        var result=new OwnerClient(http,id);await result.RefreshCsrfAsync();return result;
    }
    public Task StepImportAsync()=>App.Services.GetRequiredService<ImportWorker>().ProcessNextAsync(CancellationToken.None);
    private void Set(string key,string value){previous[key]=Environment.GetEnvironmentVariable(key);Environment.SetEnvironmentVariable(key,value);}
    private static string FindRoot(){var path=new DirectoryInfo(AppContext.BaseDirectory);while(path is not null&&!File.Exists(Path.Combine(path.FullName,"WhatHaveIDone.slnx")))path=path.Parent;return path?.FullName??throw new InvalidOperationException("Repository root not found.");}
    public async Task DisposeAsync()
    {
        if(App is not null)await App.DisposeAsync();
        foreach(var pair in previous)Environment.SetEnvironmentVariable(pair.Key,pair.Value);
        if(!createdDatabase)return;
        await using var admin=new NpgsqlConnection(connection);await admin.OpenAsync();
        if(!database.StartsWith("whid_tests_",StringComparison.Ordinal)||database.Length!=43)throw new InvalidOperationException("Refusing to drop an unexpected database.");
        await using var command=new NpgsqlCommand("DROP DATABASE "+database+" WITH (FORCE)",admin);await command.ExecuteNonQueryAsync();
    }
}
