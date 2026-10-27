using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WhatHaveIDone.Api.Tests;

public sealed class FakeTogglHandler : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string,int> calls=[];
    public ConcurrentQueue<string> ReportBodies { get; }=new();
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        var token=Encoding.UTF8.GetString(Convert.FromBase64String(request.Headers.Authorization!.Parameter!)).Split(':')[0];
        var path=request.RequestUri!.AbsolutePath;
        if(path=="/api/v9/me")
        {
            var count=calls.AddOrUpdate(token,1,(_,value)=>value+1);
            if(token.StartsWith("throttled",StringComparison.Ordinal)&&count==1)
            {var response=Json(new{error="Too many requests"},HttpStatusCode.TooManyRequests);response.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromMinutes(2));return response;}
            if(token.StartsWith("quota",StringComparison.Ordinal)&&count==1)
            {var response=Json(new{error="API limit exceeded"},HttpStatusCode.PaymentRequired);response.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromHours(1));return response;}
            if(token.StartsWith("invalid",StringComparison.Ordinal))return Json(new{error="invalid"},HttpStatusCode.Unauthorized);
            return Json(new{id=token.StartsWith("other",StringComparison.Ordinal)?43:42,email="toggl-owner@example.test",timezone="Europe/Prague"});
        }
        if(path.EndsWith("/me/workspaces",StringComparison.Ordinal))return Json(new{items=new[]{new{id=7,name="Source workspace"}}});
        if(path.EndsWith("/tags",StringComparison.Ordinal))return Json(new{items=new[]{new{id=11,name=" Imported "},new{id=12,name="Unused metadata tag"}}});
        if(path.EndsWith("/projects",StringComparison.Ordinal))return Json(new[]{new{id=22,name="Imported project",color="#009cde",active=true}});
        if(path.EndsWith("/search/time_entries",StringComparison.Ordinal))
        {
            var body=await request.Content!.ReadAsStringAsync(cancellationToken);ReportBodies.Enqueue(body);
            if(token.StartsWith("premium",StringComparison.Ordinal))return Json(new{error="Workspace needs to have this feature enabled"},HttpStatusCode.PaymentRequired);
            using var json=JsonDocument.Parse(body);var next=json.RootElement.TryGetProperty("first_row_number",out var row)&&row.ValueKind==JsonValueKind.Number?row.GetInt32():0;
            if(next==0)
            {
                var response=Json(new[]{Group(1002,42,"2026-10-26T09:00:00Z"),Group(9999,99,"2026-10-26T10:00:00Z")});
                response.Headers.Add("X-Next-Row-Number","2");response.Headers.Add("X-Next-ID","1001");return response;
            }
            return Json(new[]{Group(1001,42,"2020-01-15T09:00:00Z"),Group(1002,42,"2026-10-26T09:00:00Z")});
        }
        return Json(new{error="Unexpected synthetic request"},HttpStatusCode.NotFound);
    }
    private static object Group(long id,int user,string start)=>new{user_id=user,project_id=22,project_name="Imported project",description="Imported work",tag_ids=new[]{11},time_entries=new[]{new{id,start,stop=DateTimeOffset.Parse(start).AddHours(1).ToString("O"),seconds=3600}}};
    private static HttpResponseMessage Json(object value,HttpStatusCode status=HttpStatusCode.OK)=>new(status){Content=new StringContent(JsonSerializer.Serialize(value),Encoding.UTF8,"application/json")};
}
