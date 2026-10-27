using Marten;
using Shared.Infrastructure.Persistence.Specifications;
using WhatHaveIDone.Models;

namespace WhatHaveIDone.Logic.Import;
public sealed class EntryCompatibilityIdSpecification(string owner,long id) : ISpecification<TimeEntry>
{
    public Task<TimeEntry?> ApplyAsync(IQueryable<TimeEntry> queryable,CancellationToken cancellationToken=default)=>queryable.Where(e=>e.OwnerId==owner&&e.CompatibilityId==id).FirstOrDefaultAsync(cancellationToken);
}
