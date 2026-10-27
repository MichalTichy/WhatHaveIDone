using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WhatHaveIDone.Logic.Import;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Import;

public sealed class ImportStorage(IConfiguration configuration,IHostEnvironment environment,ILogger<ImportStorage> logger)
{
    public static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web);
    public string Root { get; } = Path.GetFullPath(configuration["Import:Path"]??Path.Combine(environment.ContentRootPath,"data","imports"));
    public string NewPath(){Directory.CreateDirectory(Root);return Path.Combine(Root,Guid.NewGuid().ToString("N")+".jsonl");}
    public bool OwnsPath(string path)=>string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)),Root,StringComparison.OrdinalIgnoreCase)&&Regex.IsMatch(Path.GetFileName(path),"^[a-f0-9]{32}\\.jsonl$");
    public void Delete(string? path)
    {
        if(path is null||!OwnsPath(path))return;
        try{File.Delete(path);}catch(Exception exception)when(exception is IOException or UnauthorizedAccessException){logger.LogWarning(exception,"An import staging file could not be removed.");}
    }
    public void CleanOrphans(IReadOnlyList<string> activeFiles, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(Root)) return;
        var retained = activeFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(Root, "*.jsonl"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (OwnsPath(path) && !retained.Contains(path) && File.GetLastWriteTimeUtc(path) < now.AddDays(-1).UtcDateTime) Delete(path);
        }
    }
    public async Task<ImportBatch> ReadBatchAsync(ImportRun run,CancellationToken cancellationToken)
    {
        if(run.FilePath is null||!OwnsPath(run.FilePath))throw new TogglImportException("InvalidFile");
        await using var file=new FileStream(run.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete,8192,true);file.Seek(run.FileOffset,SeekOrigin.Begin);
        using var reader=new StreamReader(file,Encoding.UTF8,false,8192);
        List<ImportedEntry> rows=[];
        for(var i=0;i<250;i++)
        {
            var line=await reader.ReadLineAsync(cancellationToken);if(line is null)break;
            run.FileOffset+=Encoding.UTF8.GetByteCount(line)+1;run.FileRow++;
            var row=JsonSerializer.Deserialize<ImportedEntry>(line,JsonOptions)??throw new TogglImportException("InvalidFile");rows.Add(row);
            if(row.ErrorCode is null)run.RangeStart=row.Start;
        }
        run.Status=run.FileOffset>=file.Length?"Completed":"Running";if(run.Status=="Completed")run.Stage="Done";
        return new ImportBatch(run,[],rows);
    }
}
