using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Shared.Infrastructure.CurrentUserProvider;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Import;
using WhatHaveIDone.Contracts;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Import;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Endpoints;

public static class ImportEndpoints
{
    public static void MapImportEndpoints(this WebApplication app)
    {
        var api=app.MapGroup("/api/v1/imports").RequireAuthorization();
        api.MapGet("",async(ICurrentUserIdProvider actor,IReadOnlyRepository<ImportRun> imports,CancellationToken ct)=>await imports.ListAsync(new ImportsSpecification(await actor.GetCurrentUserIdRequiredAsync()),ct));
        api.MapPost("/prepare",async(PrepareApiImport input,ISender sender,IDataProtectionProvider protection,CancellationToken ct)=>
        {
            if(string.IsNullOrWhiteSpace(input.Token)||input.Token.Length>256)throw new AppException("InvalidImport");
            var token=protection.CreateProtector(TogglImportClient.ProtectionPurpose).Protect(input.Token.Trim());
            return Results.Ok(await sender.Send(new PrepareImportUsecase("Api",token),ct));
        });
        api.MapPost("/file",async(HttpRequest request,TogglCsvReader reader,ImportStorage storage,ISender sender,CancellationToken ct)=>
        {
            var form=await request.ReadFormAsync(ct);var file=form.Files.GetFile("file")??throw new AppException("InvalidFile");
            var preview=await reader.PrepareAsync(file,form["timeZone"].ToString(),ct);
            try{return Results.Ok(await sender.Send(new PrepareImportUsecase("Csv",null,preview.Path,preview.Hash,form["timeZone"],preview.Emails,preview.Rows,preview.HasOriginalIds,preview.Preview,preview.Errors),ct));}
            catch{storage.Delete(preview.Path);throw;}
        });
        api.MapPost("/{id}/confirm",async(string id,ConfirmImportInput input,ISender sender,CancellationToken ct)=>Results.Ok(await sender.Send(new ConfirmImportUsecase(id,input),ct)));
        api.MapPost("/{id}/cancel",async(string id,ISender sender,ImportStorage storage,CancellationToken ct)=>{var path=await sender.Send(new CancelImportUsecase(id),ct);storage.Delete(path);return Results.Ok(true);});
        api.MapPost("/{id}/resume",async(string id,PrepareApiImport input,ISender sender,IDataProtectionProvider protection,CancellationToken ct)=>
        {
            if(input.Token.Length>256)throw new AppException("InvalidImport");
            var token=string.IsNullOrWhiteSpace(input.Token)?null:protection.CreateProtector(TogglImportClient.ProtectionPurpose).Protect(input.Token.Trim());
            return Results.Ok(await sender.Send(new ResumeImportUsecase(id,token),ct));
        });
    }
}
