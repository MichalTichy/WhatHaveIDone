namespace Shared.Infrastructure.Persistence.Marten.UnitOfWorks;

public enum UnitOfWorkState
{
    Active,
    Committing,
    Committed,
    Failed,
    RolledBack
}
