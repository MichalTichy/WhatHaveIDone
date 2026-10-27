using System.Net.Http.Json;
using WhatHaveIDone.Contracts;

namespace WhatHaveIDone.Api.Tests;

public sealed class OwnerClient(HttpClient http,string owner) : IDisposable
{
    public HttpClient Http { get; }=http;
    public string Owner { get; }=owner;
    public async Task<T> GetAsync<T>(string path)
    {using var response=await Http.GetAsync("/api/v1/"+path);response.EnsureSuccessStatusCode();return(await response.Content.ReadFromJsonAsync<T>())!;}
    public async Task<T> PostAsync<T>(string path,object? input)
    {using var response=await Http.PostAsJsonAsync("/api/v1/"+path,input);response.EnsureSuccessStatusCode();return(await response.Content.ReadFromJsonAsync<T>())!;}
    public async Task<T> PutAsync<T>(string path,object input)
    {using var response=await Http.PutAsJsonAsync("/api/v1/"+path,input);response.EnsureSuccessStatusCode();return(await response.Content.ReadFromJsonAsync<T>())!;}
    public async Task RefreshCsrfAsync()
    {var session=await GetAsync<SessionModel>("session");Http.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");Http.DefaultRequestHeaders.Add("X-CSRF-TOKEN",session.CsrfToken);}
    public void Dispose()=>Http.Dispose();
}
