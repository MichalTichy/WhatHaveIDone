using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Execution;

public static class OwnedDocuments
{
    public static async Task<T> RequireAsync<T>(this IReadOnlyRepository<T> repository, string owner, string id, CancellationToken cancellationToken) where T : OwnedDocument
        => await repository.GetBySpecAsync(new OwnedByIdSpecification<T>(owner, id), cancellationToken) ?? throw new AppException("NotFound", 404);

    public static void CheckVersion(OwnedDocument document, Guid expected)
    {
        if (document.Version != expected) throw new AppException("Conflict", 409);
    }
}
