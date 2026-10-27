namespace Shared.Infrastructure.Persistence.Marten.UnitOfWorks;

public class UnitOfWorkProvider : IAsyncDisposable
{
    private static readonly AsyncLocal<AmbientContext?> Data = new();
    private readonly UnitOfWork unitOfWork;
    private readonly AmbientContext context;

    public bool IsOwner { get; }
    public static UnitOfWork? Current => Data.Value?.Value;

    public UnitOfWorkProvider()
    {
        if (Current == null)
        {
            unitOfWork = new UnitOfWork(Guid.NewGuid());
            context = new AmbientContext { Value = unitOfWork };
            Data.Value = context;
            IsOwner = true;
        }
        else
        {
            context = Data.Value!;
            unitOfWork = context.Value!;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!IsOwner) return;
        Detach();
        await unitOfWork.DisposeAsync();
    }

    public UnitOfWork Get()
    {
        return unitOfWork;
    }

    public Task CommitAsync() => CommitAsync(CancellationToken.None);

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (!IsOwner) return;
        await unitOfWork.CommitAsync(cancellationToken);
        Detach();
        await unitOfWork.NotifySuccessfulCommitAsync();
    }

    private void Detach()
    {
        if (ReferenceEquals(context.Value, unitOfWork)) context.Value = null;
    }

    // AsyncLocal assignments inside async methods do not flow back to the caller's execution context.
    // Contexts that inherited this holder still share the same object, so clearing Value during
    // commit/disposal detaches the completed UOW for all of them. Assigning Data.Value = null instead
    // would leave the caller pointing at the old UOW, allowing a later provider to reuse it.
    // This indirection lets completion callbacks and subsequent operations open a fresh UOW.
    private sealed class AmbientContext
    {
        public UnitOfWork? Value { get; set; }
    }
}
