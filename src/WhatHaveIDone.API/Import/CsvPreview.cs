using WhatHaveIDone.Models;
namespace WhatHaveIDone.API.Import;
public sealed record CsvPreview(string Path,string Hash,List<string> Emails,int Rows,bool HasOriginalIds,List<ImportPreviewItem> Preview,int Errors);
