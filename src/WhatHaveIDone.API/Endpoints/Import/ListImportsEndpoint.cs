using FastEndpoints;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.API.Models;
using WhatHaveIDone.Logic.Import;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.API.Endpoints;

internal sealed class ListImportsEndpoint(IReadOnlyRepository<ImportRun> imports) : EndpointWithoutRequest<IReadOnlyList<ImportModel>>
{
    public override void Configure() => Get("/api/v1/imports");

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(await imports.ListAsync(new ImportsSpecification(), ct), ct);
    }
}
