using FastEndpoints;
using MediatR;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Import;
using Microsoft.AspNetCore.DataProtection;
using WhatHaveIDone.API.Import;
using WhatHaveIDone.Logic.Execution;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class ResumeImportEndpoint(ISender sender, IDataProtectionProvider protection, TogglImportClient toggl, TimeProvider clock) : Endpoint<PrepareApiImport, bool>
{
    public override void Configure() => Post("/api/v1/imports/{id}/resume");

    public override async Task HandleAsync(PrepareApiImport request, CancellationToken ct)
    {
        if (request.Token?.Length > 256) throw new AppException("InvalidImport");
        string? token = null;
        long? sourceUserId = null;
        if (!string.IsNullOrWhiteSpace(request.Token))
        {
            var source = await ImportSource.ReadAsync(toggl, request.Token.Trim(), clock, HttpContext.Response, ct);
            sourceUserId = source.UserId;
            token = protection.CreateProtector(TogglImportClient.ProtectionPurpose).Protect(request.Token.Trim());
        }
        await Send.OkAsync(await sender.Send(new ResumeImportUsecase(Route<string>("id")!, token, sourceUserId), ct), ct);
    }
}
