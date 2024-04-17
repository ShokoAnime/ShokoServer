using System;
using System.Threading;
using System.Threading.Tasks;
using Quartz.Impl.AdoJobStore;

namespace Shoko.Server.Scheduling.DatabaseLocks;

public class NoLockSemaphore : ISemaphore
{
    public ValueTask<bool> ObtainWriteLock(Guid requestorId, ConnectionAndTransactionHolder conn, string lockName,
        CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask<bool>(true);
    }

    public ValueTask<bool> ObtainReadLock(Guid requestorId, ConnectionAndTransactionHolder conn, string lockName,
        CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask<bool>(true);
    }

    public ValueTask ReleaseWriteLock(Guid requestorId, string lockName, CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    public ValueTask ReleaseReadLock(Guid requestorId, string lockName, CancellationToken cancellationToken = new CancellationToken())
    {
        return new ValueTask();
    }

    public bool RequiresConnection => false;
}
