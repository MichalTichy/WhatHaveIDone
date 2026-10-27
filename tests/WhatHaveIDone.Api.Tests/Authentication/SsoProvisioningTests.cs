using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Infrastructure.Identity;
using WhatHaveIDone.Logic.Authentication;
using WhatHaveIDone.Logic.Execution;
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
            var sender=services.GetRequiredService<ISender>();var email=Guid.NewGuid().ToString("N")+"@example.test";
            var first=await sender.Send(new CompleteSsoUsecase(allowed,oid,email,"Synthetic SSO account"));
            Assert.Equal(first,await sender.Send(new CompleteSsoUsecase(allowed,oid,"changed@example.test","Changed")));
            await using (var read = services.GetRequiredService<IDocumentStore>().QuerySession())
            {
                var client = Assert.Single(await read.Query<Client>().Where(c => c.OwnerId == first).ToListAsync());
                Assert.True(client.IsDefault);
                Assert.Equal(client.Id, (await read.LoadAsync<UserProfile>(first))!.DefaultClientId);
            }
            var rejected=await Assert.ThrowsAsync<AppException>(()=>sender.Send(new CompleteSsoUsecase(Guid.NewGuid().ToString(),Guid.NewGuid().ToString(),email,"Other tenant")));Assert.Equal("SsoDenied",rejected.Code);
            var collision=await Assert.ThrowsAsync<AppException>(()=>sender.Send(new CompleteSsoUsecase(allowed,Guid.NewGuid().ToString(),email,"Same email")));Assert.Equal("SsoAccountConflict",collision.Code);
            var manager=services.GetRequiredService<UserManager<AppUser>>();Assert.Equal(first,(await manager.FindByLoginAsync("Entra",allowed+":"+oid))!.Id);
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
