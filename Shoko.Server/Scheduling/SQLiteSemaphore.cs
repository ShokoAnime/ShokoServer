using System;
using System.Threading;
using System.Threading.Tasks;
using Quartz.Impl.AdoJobStore;

namespace Shoko.Server.Scheduling;

public class SQLiteSemaphore : ISemaphore
{
    private readonly SemaphoreSlim _lock = new(1,1);

    public async Task<bool> ObtainLock(Guid requestorId, ConnectionAndTransactionHolder conn, string lockName, CancellationToken cancellationToken = new CancellationToken())
    {
        await _lock.WaitAsync(cancellationToken);
        return true;
    }

    public Task ReleaseLock(Guid requestorId, string lockName, CancellationToken cancellationToken = new CancellationToken())
    {
        _lock.Release();
        return Task.CompletedTask;
    }

    public bool RequiresConnection => false;
}
