using System;
using System.Threading;
using System.Threading.Tasks;
using Quartz.Impl.AdoJobStore;

namespace Shoko.Server.Scheduling.DatabaseLocks;

public class SQLiteSemaphore : ISemaphore
{
    private readonly SemaphoreSlim _lock = new(1,1);
    private Guid? _owner;

    public async Task<bool> ObtainLock(Guid requestorId, ConnectionAndTransactionHolder conn, string lockName, CancellationToken cancellationToken = new CancellationToken())
    {
        if (_owner.HasValue && _owner.Value.Equals(requestorId)) return true;
        await _lock.WaitAsync(cancellationToken);
        _owner = requestorId;
        return true;
    }

    public Task ReleaseLock(Guid requestorId, string lockName, CancellationToken cancellationToken = new CancellationToken())
    {
        _lock.Release();
        _owner = null;
        return Task.CompletedTask;
    }

    public bool RequiresConnection => false;
}
