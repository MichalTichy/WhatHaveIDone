using System.Security.Cryptography;
using System.Text;
using Shared.Infrastructure.Persistence.Repositories;
using WhatHaveIDone.Logic.Execution;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Tags;

public sealed class TagCatalog(IRepository<Tag> tags)
{
    public async Task<List<string>> ResolveAsync(UserProfile profile, IEnumerable<string> names, CancellationToken cancellationToken, IReadOnlyDictionary<string,long>? originalIds = null)
    {
        if (names.Any(n => n is null)) throw new AppException("InvalidTags");
        var distinct = names.Select(n => n.Trim().Normalize()).Where(n => n.Length > 0).DistinctBy(Tag.Normalize).ToArray();
        if (distinct.Length > 30 || distinct.Any(n => n.Length > 100)) throw new AppException("InvalidTags");
        List<string> result = [];
        foreach (var name in distinct)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = Tag.Normalize(name);
            var id = profile.Id + ":tag:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
            var existing = await tags.GetBySpecAsync(new OwnedByIdSpecification<Tag>(profile.Id, id), cancellationToken);
            if (existing is null)
            {
                existing = new Tag { Id = id, OwnerId = profile.Id, CompatibilityId = originalIds is not null && originalIds.TryGetValue(normalized,out var original) ? original : profile.AllocateId(), Name = name, NormalizedName = normalized };
                await tags.AddAsync(existing, cancellationToken);
            }
            result.Add(existing.Name);
        }
        return result;
    }
}
