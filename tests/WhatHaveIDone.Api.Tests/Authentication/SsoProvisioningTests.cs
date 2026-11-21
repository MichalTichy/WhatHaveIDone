using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Infrastructure.Identity;
using Marten;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Api.Tests;

[Collection("postgres")]
public sealed class SsoProvisioningTests(IntegrationFixture fixture)
{
    [Fact] public async Task StableProviderIdentityIsReusedAndNeitherOtherTenantsNorEmailOnlyLinkingAreAcceptedAsync()
    {
        await using var scope=fixture.App.Services.CreateAsyncScope();var services=scope.ServiceProvider;
        var configuration=services.GetRequiredService<IConfiguration>();var accessor=services.GetRequiredService<IHttpContextAccessor>();var previous=accessor.HttpContext;
        var enabled=configuration["Sso:Enabled"];var tenant=configuration["AzureAd:TenantId"];var allowed=Guid.NewGuid().ToString();var oid=Guid.NewGuid().ToString();
        try
        {
            configuration["Sso:Enabled"]="true";configuration["AzureAd:TenantId"]=allowed;
            accessor.HttpContext=new DefaultHttpContext{RequestServices=services};accessor.HttpContext.Request.Scheme="http";accessor.HttpContext.Request.Host=new HostString("localhost");
            var email=Guid.NewGuid().ToString("N")+"@example.test";
            async Task<DefaultHttpContext> ReceiveAsync(string tid, string objectId, string address)
            {
                var http = new DefaultHttpContext { RequestServices = services };
                http.Request.Scheme = "http"; http.Request.Host = new HostString("localhost");
                accessor.HttpContext = http;
                var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tid", tid), new Claim("oid", objectId),
                    new Claim("email", address), new Claim("name", "Synthetic SSO account")], "Entra"));
                var ticket = new AuthenticationTicket(principal, "Entra");
                var context = new TicketReceivedContext(http, new AuthenticationScheme("Entra", "Entra", typeof(OpenIdConnectHandler)), new OpenIdConnectOptions(), ticket);
                await AccountEndpoints.HandleSsoTicketAsync(context);
                return http;
            }
            var manager=services.GetRequiredService<UserManager<AppUser>>();
            var firstTicket = await ReceiveAsync(allowed, oid, email);
            Assert.Equal("/time", firstTicket.Response.Headers.Location.ToString());
            var first = (await manager.FindByLoginAsync("Entra", allowed + ":" + oid))!.Id;
            var repeated = await ReceiveAsync(allowed, oid, "changed@example.test");
            Assert.Equal("/time", repeated.Response.Headers.Location.ToString());
            Assert.Equal(first, (await manager.FindByLoginAsync("Entra", allowed + ":" + oid))!.Id);
            await using (var read = services.GetRequiredService<IDocumentStore>().QuerySession(first))
            {
                var client = Assert.Single(await read.Query<Client>().ToListAsync());
                Assert.True(client.IsDefault);
                Assert.Equal(client.Id, (await read.LoadAsync<UserProfile>(first))!.DefaultClientId);
            }
            var otherTenant = Guid.NewGuid().ToString(); var otherObject = Guid.NewGuid().ToString();
            Assert.Equal("/login?error=SsoDenied", (await ReceiveAsync(otherTenant, otherObject, email)).Response.Headers.Location.ToString());
            Assert.Null(await manager.FindByLoginAsync("Entra", otherTenant + ":" + otherObject));
            var collisionObject = Guid.NewGuid().ToString();
            Assert.Equal("/login?error=SsoDenied", (await ReceiveAsync(allowed, collisionObject, email)).Response.Headers.Location.ToString());
            Assert.Null(await manager.FindByLoginAsync("Entra", allowed + ":" + collisionObject));
            Assert.Equal(first, (await manager.FindByEmailAsync(email))!.Id);
        }
        finally{configuration["Sso:Enabled"]=enabled;configuration["AzureAd:TenantId"]=tenant;accessor.HttpContext=previous;}
    }
    [Fact] public async Task LockedAccountsLoseExistingCookieAndTokenAccessAsync()
    {
        using var owner=await fixture.CreateOwnerAsync();var token=await owner.PostAsync<CreatedApiToken>("tokens",new CreateApiKeyRequest("Lockout"));
        await using(var scope=fixture.App.Services.CreateAsyncScope())
        {var manager=scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();var user=await manager.FindByIdAsync(owner.Owner);Assert.True((await manager.SetLockoutEndDateAsync(user!,DateTimeOffset.UtcNow.AddHours(1))).Succeeded);}
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized,(await owner.Http.GetAsync("/api/v1/projects")).StatusCode);
        using var api=fixture.App.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions{HandleCookies=false});api.DefaultRequestHeaders.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token.Token);
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized,(await api.GetAsync("/api/v1/projects")).StatusCode);
    }
}
