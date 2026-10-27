using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Import;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Import;

public sealed class TogglCsvReader(ImportStorage storage)
{
    public async Task<CsvPreview> PrepareAsync(IFormFile upload,string timeZone,CancellationToken cancellationToken)
    {
        if(upload.Length is <=0 or >20*1024*1024)throw new AppException("InvalidFile");
        TimeZoneInfo zone;try{zone=TimeZoneInfo.FindSystemTimeZoneById(timeZone);}catch(TimeZoneNotFoundException){throw new AppException("InvalidSettings");}
        await using var input=upload.OpenReadStream();
        var hash=Convert.ToHexString(await SHA256.HashDataAsync(input,cancellationToken));input.Position=0;
        var path=storage.NewPath();
        try
        {
            using var text=new StreamReader(input,new UTF8Encoding(false,true),true,8192,leaveOpen:true);
            using var csv=new CsvReader(text,new CsvConfiguration(CultureInfo.InvariantCulture){Delimiter=",",PrepareHeaderForMatch=args=>args.Header.Trim().Trim('\uFEFF').ToLowerInvariant()});
            if(!await csv.ReadAsync())throw new AppException("InvalidFile");csv.ReadHeader();
            var headers=csv.HeaderRecord?.Select(h=>h.Trim().Trim('\uFEFF').ToLowerInvariant()).ToHashSet()??[];
            if(new[]{"email","description","project","start date","start time","end date","end time","duration","tags"}.Any(h=>!headers.Contains(h)))throw new AppException("InvalidFile");
            await using var output=new StreamWriter(path,false,new UTF8Encoding(false)){NewLine="\n"};
            var emails=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var rows=0;var errors=0;var allIds=(headers.Contains("time entry id")||headers.Contains("id"))&&headers.Contains("project id");List<ImportPreviewItem> preview=[];
            while(await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();rows++;
                var email=csv.GetField("email")?.Trim()??"";if(email.Length>0)emails.Add(email);
                var description=csv.GetField("description")??"";var project=csv.GetField("project")??"";var tagText=csv.GetField("tags")??"";
                var idText=headers.Contains("time entry id")?csv.GetField("time entry id"):headers.Contains("id")?csv.GetField("id"):null;
                var projectText=headers.Contains("project id")?csv.GetField("project id"):null;
                long? id=null,pid=null;DateTimeOffset start=default,end=default;string? error=null;
                try
                {
                    if(!string.IsNullOrWhiteSpace(idText)){id=long.Parse(idText,CultureInfo.InvariantCulture);if(id<=0)throw new FormatException();}else allIds=false;
                    if(!string.IsNullOrWhiteSpace(projectText)){pid=long.Parse(projectText,CultureInfo.InvariantCulture);if(pid<=0)throw new FormatException();}else if(!string.IsNullOrWhiteSpace(project))allIds=false;
                    (start,end)=ParseInterval(csv.GetField("start date")??"",csv.GetField("start time")??"",csv.GetField("end date")??"",csv.GetField("end time")??"",csv.GetField("duration")??"",zone);
                }
                catch(Exception exception)when(exception is FormatException or ArgumentException or OverflowException){error="ImportFileRow";errors++;}
                var source=id.HasValue?"toggl:entry:"+id.Value:"file:"+hash+":"+rows;
                var row=new ImportedEntry(source,id,description,pid,project,tagText.Split(',').Select(t=>t.Trim()).Where(t=>t.Length>0).ToList(),start,end,SourceEmail:email,ErrorCode:error);
                await output.WriteLineAsync(JsonSerializer.Serialize(row,ImportStorage.JsonOptions).AsMemory(),cancellationToken);
                if(error is null&&preview.Count<5)preview.Add(new ImportPreviewItem(email,description,project,start,end));
            }
            if(rows==0||emails.Count==0)throw new AppException("InvalidFile");
            await output.FlushAsync(cancellationToken);return new CsvPreview(path,hash,emails.Order().ToList(),rows,allIds,preview,errors);
        }
        catch(CsvHelperException){storage.Delete(path);throw new AppException("InvalidFile");}
        catch{storage.Delete(path);throw;}
    }
    public static (DateTimeOffset Start,DateTimeOffset End) ParseInterval(string startDate,string startTime,string endDate,string endTime,string duration,TimeZoneInfo zone)
    {
        var start=DateTime.ParseExact(startDate+" "+startTime,"yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
        var end=DateTime.ParseExact(endDate+" "+endTime,"yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
        var parts=duration.Split(':');if(parts.Length!=3)throw new FormatException();
        var hours=long.Parse(parts[0],CultureInfo.InvariantCulture);var minutes=int.Parse(parts[1],CultureInfo.InvariantCulture);var seconds=int.Parse(parts[2],CultureInfo.InvariantCulture);
        if(hours<0||minutes is <0 or >59||seconds is <0 or >59)throw new FormatException();
        var expected=checked(hours*3600+minutes*60+seconds);
        var matches=(from s in Candidates(start,zone) from e in Candidates(end,zone) where e>=s&&Math.Abs((e-s).TotalSeconds-expected)<.5 select (s,e)).Distinct().ToList();
        if(matches.Count!=1)throw new FormatException();return matches[0];
    }
    private static IEnumerable<DateTimeOffset> Candidates(DateTime value,TimeZoneInfo zone)
    {
        value=DateTime.SpecifyKind(value,DateTimeKind.Unspecified);if(zone.IsInvalidTime(value))yield break;
        if(zone.IsAmbiguousTime(value)){foreach(var offset in zone.GetAmbiguousTimeOffsets(value))yield return new DateTimeOffset(value,offset).ToUniversalTime();}
        else yield return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(value,zone));
    }
}
