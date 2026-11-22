using FastEndpoints;
using MediatR;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Import;
using Microsoft.AspNetCore.DataProtection;
using WhatHaveIDone.API.Import;
using WhatHaveIDone.Logic.Execution;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class PrepareImportEndpoint(ISender sender, IDataProtectionProvider protection, TogglImportClient toggl, TimeProvider clock) : Endpoint<PrepareApiImport, string>
{
    public override void Configure() => Post("/api/v1/imports/prepare");

    public override async Task HandleAsync(PrepareApiImport request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 256) throw new AppException("InvalidImport");
        var source = await ImportSource.ReadAsync(toggl, request.Token.Trim(), clock, HttpContext.Response, ct);
        var token = protection.CreateProtector(TogglImportClient.ProtectionPurpose).Protect(request.Token.Trim());
        await Send.OkAsync(await sender.Send(new PrepareImportUsecase(token, source, request.From, request.Through), ct), ct);
    }
}
