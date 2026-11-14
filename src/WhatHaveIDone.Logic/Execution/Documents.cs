using Shared.Infrastructure.Persistence.Repositories;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Execution;

public static class Documents
{
    public static async Task<T> RequireAsync<T>(this IReadOnlyRepository<T> repository, ISpecification<T> specification, CancellationToken cancellationToken) where T : Document
        => await repository.GetBySpecAsync(specification, cancellationToken) ?? throw new AppException("NotFound", 404);

    public static async Task<T> RequireAsync<T>(this IReadOnlyRepository<T> repository, string id, CancellationToken cancellationToken, string? tenantId = null) where T : Document
        => await repository.GetByIdAsync(id, cancellationToken, tenantId) ?? throw new AppException("NotFound", 404);

    public static void CheckVersion(Document document, Guid expected)
    {
        if (document.Version != expected) throw new AppException("Conflict", 409);
    }
}
