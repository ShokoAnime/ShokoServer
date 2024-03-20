using System;
using System.Threading;
using System.Threading.Tasks;
using Quartz.Impl.AdoJobStore;

namespace Shoko.Server.Scheduling.DatabaseLocks;

public class NoLockSemaphore : ISemaphore
{
    public Task<bool> ObtainLock(Guid requestorId, ConnectionAndTransactionHolder conn, string lockName, CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.FromResult(true);
    }

    public Task ReleaseLock(Guid requestorId, string lockName, CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.CompletedTask;
    }

    public bool RequiresConnection => false;
}
