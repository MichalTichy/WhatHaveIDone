using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WhatHaveIDone.Web;
using WhatHaveIDone.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddBlazorBlueprintComponents(configureTheme: options =>
{
    options.DetectSystemPreference = false;
    options.PersistToLocalStorage = false;
});
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<UserContext>();
builder.Services.AddScoped<Feedback>();
await builder.Build().RunAsync();
