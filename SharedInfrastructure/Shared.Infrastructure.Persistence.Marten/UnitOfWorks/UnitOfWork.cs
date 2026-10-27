using System.Data;
using System.Diagnostics;
using JetBrains.Annotations;
using Npgsql;

namespace Shared.Infrastructure.Persistence.Marten.UnitOfWorks;

public class UnitOfWork : IAsyncDisposable
{
    private readonly List<Func<CancellationToken, Task>> beforeCommit = [];
    private readonly List<Func<CancellationToken, Task>> successfulCommit = [];
    private readonly List<Func<CancellationToken, Task>> rollback = [];
    private readonly List<Exception> completionErrors = [];
    private bool disposed;
    private bool notifiedCommit;

    [UsedImplicitly]
    internal string CreationStackTrace { get; }

    public Guid OwnerId { get; }

    public UnitOfWorkState State { get; private set; }
    public Exception? Failure { get; private set; }
    public IReadOnlyList<Exception> CompletionErrors => completionErrors.AsReadOnly();

    internal NpgsqlConnection? Connection { get; private set; }
    internal NpgsqlTransaction? Transaction { get; private set; }

    internal UnitOfWork(Guid ownerId)
    {
        OwnerId = ownerId;
#if DEBUG
        CreationStackTrace = new StackTrace().ToString();
#else
        CreationStackTrace = "Only in debug";
#endif
    }

    /// <summary>Runs inside the ambient transaction, before its actual commit.</summary>
    public void OnBeforeCommit(Func<CancellationToken, Task> callback) => Register(beforeCommit, callback);

    /// <summary>Runs once after commit, outside the completed ambient unit of work.</summary>
    public void OnSuccessfulCommit(Func<CancellationToken, Task> callback) => Register(successfulCommit, callback);

    /// <summary>Runs once after rollback and resource disposal, outside the aborted ambient unit of work.</summary>
    public void OnRollback(Func<CancellationToken, Task> callback) => Register(rollback, callback);

    /// <summary>Prevents committing, including when a nested operation failed and its exception was handled.</summary>
    public void Abort(Exception? failure = null)
    {
        if (State is UnitOfWorkState.Committed or UnitOfWorkState.RolledBack)
            throw new InvalidOperationException("The unit of work has already completed.");
        Failure ??= failure;
        State = UnitOfWorkState.Failed;
    }

    private void Register(List<Func<CancellationToken, Task>> callbacks, Func<CancellationToken, Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (State != UnitOfWorkState.Active || disposed)
            throw new InvalidOperationException("Register callbacks before committing the unit of work.");
        callbacks.Add(callback);
    }

    internal async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (State == UnitOfWorkState.Committed) return;
        if (State != UnitOfWorkState.Active || disposed)
            throw new InvalidOperationException("The unit of work cannot be committed.", Failure);
        State = UnitOfWorkState.Committing;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var callback in beforeCommit)
            {
                await callback(cancellationToken);
                if (State == UnitOfWorkState.Failed)
                    throw new InvalidOperationException("The unit of work was aborted before commit.", Failure);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (Transaction != null) await Transaction.CommitAsync(cancellationToken);
            State = UnitOfWorkState.Committed;
        }
        catch (Exception exception)
        {
            Abort(exception);
            throw;
        }
    }

    internal async Task NotifySuccessfulCommitAsync()
    {
        if (notifiedCommit) return;
        notifiedCommit = true;
        var errors = await InvokeCompletionCallbacksAsync(successfulCommit);
        if (errors.Count > 0)
            throw new AggregateException("The transaction committed, but a completion callback failed.", errors);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        var committed = State == UnitOfWorkState.Committed;
        try
        {
            if (Transaction != null)
            {
                if (!committed)
                {
                    try { await Transaction.RollbackAsync(); }
                    catch (Exception exception) { completionErrors.Add(exception); }
                }
                try { await Transaction.DisposeAsync(); }
                catch (Exception exception) { completionErrors.Add(exception); }
            }
        }
        finally
        {
            if (Connection != null)
            {
                try { await Connection.DisposeAsync(); }
                catch (Exception exception) { completionErrors.Add(exception); }
            }
        }
        if (!committed)
        {
            State = UnitOfWorkState.RolledBack;
            await InvokeCompletionCallbacksAsync(rollback);
        }
    }

    private async Task<List<Exception>> InvokeCompletionCallbacksAsync(List<Func<CancellationToken, Task>> callbacks)
    {
        List<Exception> errors = [];
        foreach (var callback in callbacks)
        {
            try { await callback(CancellationToken.None); }
            catch (Exception exception) { errors.Add(exception); completionErrors.Add(exception); }
        }
        return errors;
    }

    internal async Task EnsureInitializedAsync(Func<CancellationToken, Task<NpgsqlConnection>> connectionFactory, IsolationLevel preferredIsolationLevel, CancellationToken cancellationToken = default)
    {
        if (disposed || State is not (UnitOfWorkState.Active or UnitOfWorkState.Committing))
            throw new InvalidOperationException("The unit of work has already completed.");
        if (Connection == null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Connection = await connectionFactory(cancellationToken);
            if (Connection.State != ConnectionState.Open)
            {
                throw new ArgumentException("Created connection is not open.", nameof(connectionFactory));
            }

            Transaction = await Connection.BeginTransactionAsync(preferredIsolationLevel, cancellationToken);
        }
    }
}
