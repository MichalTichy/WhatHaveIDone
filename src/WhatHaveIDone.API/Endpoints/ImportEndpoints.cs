using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Import;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Logic.Import;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Endpoints;

public static class ImportEndpoints
{
    public static void MapImportEndpoints(this WebApplication app)
    {
        var api=app.MapGroup("/api/v1/imports").RequireAuthorization();
        api.MapGet("",async(IReadOnlyRepository<ImportRun> imports,CancellationToken ct)=>await imports.ListAsync(new ImportsSpecification(),ct));
        api.MapPost("/prepare",async(PrepareApiImport input,ISender sender,IDataProtectionProvider protection,TogglImportClient toggl,TimeProvider clock,HttpContext context,CancellationToken ct)=>
        {
            if (string.IsNullOrWhiteSpace(input.Token) || input.Token.Length > 256) throw new AppException("InvalidImport");
            var source = await ReadSourceAsync(toggl, input.Token.Trim(), clock, context.Response, ct);
            var token = protection.CreateProtector(TogglImportClient.ProtectionPurpose).Protect(input.Token.Trim());
            return Results.Ok(await sender.Send(new PrepareImportUsecase(token, source, input.From, input.Through), ct));
        });
        api.MapPost("/{id}/confirm",async(string id,ConfirmImportInput input,ISender sender,CancellationToken ct)=>Results.Ok(await sender.Send(new ConfirmImportUsecase(id,input),ct)));
        api.MapPost("/{id}/cancel",async(string id,ISender sender,CancellationToken ct)=>Results.Ok(await sender.Send(new CancelImportUsecase(id),ct)));
        api.MapPost("/{id}/resume",async(string id,PrepareApiImport input,ISender sender,IDataProtectionProvider protection,TogglImportClient toggl,TimeProvider clock,HttpContext context,CancellationToken ct)=>
        {
            if (input.Token?.Length > 256) throw new AppException("InvalidImport");
            string? token = null;
            long? sourceUserId = null;
            if (!string.IsNullOrWhiteSpace(input.Token))
            {
                var source = await ReadSourceAsync(toggl, input.Token.Trim(), clock, context.Response, ct);
                sourceUserId = source.UserId;
                token = protection.CreateProtector(TogglImportClient.ProtectionPurpose).Protect(input.Token.Trim());
            }
            return Results.Ok(await sender.Send(new ResumeImportUsecase(id, token, sourceUserId), ct));
        });
    }

    private static async Task<TogglImportSource> ReadSourceAsync(TogglImportClient toggl, string token, TimeProvider clock, HttpResponse response,
        CancellationToken cancellationToken)
    {
        try { return await toggl.GetSourceAsync(token, cancellationToken); }
        catch (TogglImportException exception)
        {
            if (exception.NextAttempt is { } next)
            {
                response.Headers.RetryAfter = Math.Max(1, Math.Ceiling((next - clock.GetUtcNow()).TotalSeconds))
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
                throw new AppException("TooManyRequests", 429);
            }
            throw new AppException(exception.Code);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
            exception is HttpRequestException or System.Text.Json.JsonException or OperationCanceledException)
        {
            throw new AppException("ImportSource", 502);
        }
    }
}
