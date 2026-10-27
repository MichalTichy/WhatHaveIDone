using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using WhatHaveIDone.Logic.Import;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Import;

public sealed class TogglImportClient(HttpClient http,IDataProtectionProvider protection,TimeProvider clock)
{
    public const string ProtectionPurpose="WhatHaveIDone.TogglImport.v1";
    public async Task<ImportBatch> FetchAsync(ImportRun run,CancellationToken cancellationToken)
    {
        var token=protection.CreateProtector(ProtectionPurpose).Unprotect(run.ProtectedToken??throw new TogglImportException("ImportToken"));
        List<ImportedProject> projects=[];List<ImportedEntry> entries=[];
        switch(run.Stage)
        {
            case "VerifyProfile":
            {
                using var response=await SendAsync(run,token,HttpMethod.Get,"api/v9/me",null,cancellationToken);
                using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                if(Long(json.RootElement,"id")!=run.SourceUserId)throw new TogglImportException("ImportToken");
                run.Stage=run.ResumeStage??(run.Confirmed?"Tags":"Workspaces");run.ResumeStage=null;break;
            }
            case "Profile":
            {
                using var response=await SendAsync(run,token,HttpMethod.Get,"api/v9/me",null,cancellationToken);
                using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                run.SourceUserId=Long(json.RootElement,"id")??throw new TogglImportException("ImportSource");run.SourceEmail=Text(json.RootElement,"email");
                var zone=Text(json.RootElement,"timezone");
                if(!string.IsNullOrWhiteSpace(zone)){try{_=TimeZoneInfo.FindSystemTimeZoneById(zone);run.SourceTimeZone=zone;}catch(TimeZoneNotFoundException){throw new TogglImportException("ImportSource");}}
                run.Stage="Workspaces";break;
            }
            case "Workspaces":
            {
                using var response=await SendAsync(run,token,HttpMethod.Get,"api/v9/me/workspaces",null,cancellationToken);
                using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                run.Workspaces=Array(json.RootElement).Select(w=>new ImportWorkspace(Long(w,"id")??throw new TogglImportException("ImportSource"),Text(w,"name"))).ToList();
                run.WorkspaceIds=run.Workspaces.Select(w=>w.Id).ToList();run.Status="AwaitingConfirmation";run.Stage="Ready";break;
            }
            case "Tags":
            {
                var workspace=run.WorkspaceIds[run.WorkspaceIndex];
                using var response=await SendAsync(run,token,HttpMethod.Get,$"api/v9/workspaces/{workspace}/tags?page={run.Page}&per_page=200",null,cancellationToken);
                using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));var rows=Array(json.RootElement).ToList();
                foreach(var row in rows){cancellationToken.ThrowIfCancellationRequested();if(Long(row,"id") is { } id&&!string.IsNullOrWhiteSpace(Text(row,"name"))){run.TagNames[id]=Text(row,"name");}}
                if(rows.Count<200){run.Page=1;run.Stage="Projects";}else run.Page++;break;
            }
            case "Projects":
            {
                var workspace=run.WorkspaceIds[run.WorkspaceIndex];
                using var response=await SendAsync(run,token,HttpMethod.Get,$"api/v9/workspaces/{workspace}/projects?active=both&page={run.Page}&per_page=200",null,cancellationToken);
                using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));var rows=Array(json.RootElement).ToList();
                foreach(var row in rows){cancellationToken.ThrowIfCancellationRequested();projects.Add(new ImportedProject(Long(row,"id")??throw new TogglImportException("ImportSource"),Text(row,"name"),Text(row,"color"),row.TryGetProperty("active",out var active)&&active.ValueKind==JsonValueKind.False));}
                if(rows.Count<200){run.Page=1;run.Stage="Entries";}else run.Page++;break;
            }
            case "Entries":
            {
                var workspace=run.WorkspaceIds[run.WorkspaceIndex];
                var zone=TimeZoneInfo.FindSystemTimeZoneById(run.SourceTimeZone);
                var body=new Dictionary<string,object?>{["start_date"]="1970-01-01",["end_date"]=TimeZoneInfo.ConvertTime(run.RangeEnd??clock.GetUtcNow(),zone).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),["user_ids"]=new[]{run.SourceUserId},["page_size"]=1000,["order_by"]="date",["order_dir"]="DESC",["grouped"]=false,["enrich_response"]=true,["rounding"]=0,["first_id"]=run.NextId,["first_row_number"]=run.NextRow};
                using var response=await SendAsync(run,token,HttpMethod.Post,$"reports/api/v3/workspace/{workspace}/search/time_entries",body,cancellationToken);
                using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                foreach(var row in Array(json.RootElement))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var sourceUser=Long(row,"user_id")??Long(row,"uid")??run.SourceUserId;
                    var tagNames=new List<string>();var missingTag=false;
                    if(row.TryGetProperty("tags",out var names)&&names.ValueKind==JsonValueKind.Array)
                        foreach(var name in names.EnumerateArray()){var text=name.ValueKind==JsonValueKind.String?name.GetString():Text(name,"name");if(!string.IsNullOrWhiteSpace(text))tagNames.Add(text);}
                    if(tagNames.Count==0&&row.TryGetProperty("tag_ids",out var ids)&&ids.ValueKind==JsonValueKind.Array)
                        foreach(var id in ids.EnumerateArray()){if(run.TagNames.TryGetValue(id.GetInt64(),out var name))tagNames.Add(name);else missingTag=true;}
                    var rowEntries=row.TryGetProperty("time_entries",out var timeEntries)&&timeEntries.ValueKind==JsonValueKind.Array?timeEntries.EnumerateArray().ToArray():new[]{row};
                    foreach(var item in rowEntries)
                    {
                        var id=Long(item,"id")??throw new TogglImportException("ImportSource");
                        if(!DateTimeOffset.TryParse(Text(item,"start"),CultureInfo.InvariantCulture,DateTimeStyles.None,out var start))throw new TogglImportException("ImportSource");
                        DateTimeOffset? end=null;var stop=Text(item,"stop");
                        if(!string.IsNullOrWhiteSpace(stop))end=DateTimeOffset.Parse(stop,CultureInfo.InvariantCulture);
                        else if(!item.TryGetProperty("stop",out _) && Long(item,"seconds") is >=0 and var seconds)end=start.AddSeconds(seconds);
                        entries.Add(new ImportedEntry("toggl:entry:"+id,id,Text(row,"description"),Long(row,"project_id")??Long(row,"pid"),Text(row,"project_name"),tagNames,start.ToUniversalTime(),end?.ToUniversalTime(),SourceUserId:sourceUser,ErrorCode:missingTag?"ImportSource":null));
                        run.RangeStart=start.ToUniversalTime();
                    }
                }
                var nextRow=HeaderLong(response,"X-Next-Row-Number");var nextId=HeaderLong(response,"X-Next-ID");
                if(nextRow.HasValue||nextId.HasValue)
                {
                    if(nextRow==run.NextRow&&nextId==run.NextId)throw new TogglImportException("ImportSource");
                    run.NextRow=nextRow.HasValue?checked((int)nextRow.Value):null;run.NextId=nextId;
                }
                else
                {
                    run.NextRow=null;run.NextId=null;run.WorkspaceIndex++;run.Page=1;
                    run.Stage=run.WorkspaceIndex<run.WorkspaceIds.Count?"Tags":"Done";
                    if(run.Stage=="Done")run.Status="Completed";
                }
                break;
            }
            default:throw new TogglImportException("ImportSource");
        }
        run.RetryCount=0;run.NextAttemptAt=null;run.MessageCode=run.Errors>0?"ImportSource":null;
        if(run.Status is not ("Completed" or "AwaitingConfirmation"))run.Status="Running";
        return new ImportBatch(run,projects,entries);
    }
    private async Task<HttpResponseMessage> SendAsync(ImportRun run,string token,HttpMethod method,string path,object? body,CancellationToken cancellationToken)
    {
        using var request=new HttpRequestMessage(method,path);
        request.Headers.Authorization=new AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes(token+":api_token")));
        request.Headers.UserAgent.ParseAdd("WhatHaveIDone/1.0");if(body is not null)request.Content=JsonContent.Create(body,body.GetType());
        var response=await http.SendAsync(request,cancellationToken);
        if(response.IsSuccessStatusCode)return response;
        var payload=await response.Content.ReadAsStringAsync(cancellationToken);
        var metadata=response.Headers.RetryAfter is not null||HeaderLong(response,"X-RateLimit-Reset").HasValue||HeaderLong(response,"RateLimit-Reset").HasValue;
        var quota=response.StatusCode==(HttpStatusCode)429||response.StatusCode==HttpStatusCode.PaymentRequired&&(metadata||!path.StartsWith("reports/",StringComparison.Ordinal)||new[]{"quota","rate limit","api limit","limit exceeded","too many requests"}.Any(s=>payload.Contains(s,StringComparison.OrdinalIgnoreCase)));
        if(quota)
        {
            var now=clock.GetUtcNow();var next=response.Headers.RetryAfter?.Date??(response.Headers.RetryAfter?.Delta is { } delay?now+delay:now.AddSeconds(Math.Min(3600,60*Math.Pow(2,Math.Min(run.RetryCount,6)))));
            var reset=HeaderLong(response,"X-RateLimit-Reset")??HeaderLong(response,"RateLimit-Reset");if(reset.HasValue&&reset>now.ToUnixTimeSeconds())next=DateTimeOffset.FromUnixTimeSeconds(reset.Value);
            response.Dispose();throw new TogglImportException("ImportQuota",next>now?next:now.AddSeconds(1));
        }
        var code=response.StatusCode==HttpStatusCode.Unauthorized?"ImportToken":response.StatusCode==HttpStatusCode.PaymentRequired?"ImportPlan":"ImportSource";
        response.Dispose();throw new TogglImportException(code);
    }
    public static IEnumerable<JsonElement> Array(JsonElement root)
    {
        if(root.ValueKind==JsonValueKind.Array)return root.EnumerateArray().ToArray();
        if(root.ValueKind==JsonValueKind.Object&&root.TryGetProperty("items",out var items)&&items.ValueKind==JsonValueKind.Array)return items.EnumerateArray().ToArray();
        if(root.ValueKind==JsonValueKind.Object&&root.TryGetProperty("data",out var data)&&data.ValueKind==JsonValueKind.Array)return data.EnumerateArray().ToArray();
        throw new TogglImportException("ImportSource");
    }
    public static string Text(JsonElement value,string name)=>value.ValueKind==JsonValueKind.Object&&value.TryGetProperty(name,out var found)&&found.ValueKind==JsonValueKind.String?found.GetString()??"":"";
    public static long? Long(JsonElement value,string name)=>value.ValueKind==JsonValueKind.Object&&value.TryGetProperty(name,out var found)&&long.TryParse(found.ToString(),CultureInfo.InvariantCulture,out var number)?number:null;
    private static long? HeaderLong(HttpResponseMessage response,string name)=>response.Headers.TryGetValues(name,out var values)&&long.TryParse(values.FirstOrDefault(),CultureInfo.InvariantCulture,out var result)?result:null;
}
