using System;
using System.Threading;
using System.Threading.Tasks;
using Quartz.Impl.AdoJobStore;

namespace Shoko.Server.Scheduling.DatabaseLocks;

public class SQLiteSemaphore : ISemaphore
{
    private readonly object _lock = new();

    public Task<bool> ObtainLock(Guid requestorId, ConnectionAndTransactionHolder conn, string lockName, CancellationToken cancellationToken = new CancellationToken())
    {
        if (Monitor.IsEntered(_lock)) return Task.FromResult(true);
        var entered = false;
        Monitor.Enter(_lock, ref entered);
        return Task.FromResult(entered);
    }

    public Task ReleaseLock(Guid requestorId, string lockName, CancellationToken cancellationToken = new CancellationToken())
    {
        if (!Monitor.IsEntered(_lock)) return Task.CompletedTask;
        Monitor.Exit(_lock);
        return Task.CompletedTask;
    }

    public bool RequiresConnection => false;
}
