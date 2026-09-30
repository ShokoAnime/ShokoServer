using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Services;

/// <summary>
///   Two locks per metadata entry, held in memory: the entry's lock, shared by
///   every refresh and purge, and its image lock, shared by every image job
///   and purge, so an entry is never refreshed twice at once, nor refreshed
///   or given images while it is purged.
/// </summary>
/// <remarks>
///   A lock is made when first asked for and dropped once nobody holds or
///   waits for it, so only the entries in use take memory. It is not
///   re-entrant. Locks are always taken in the same order: an entry's lock,
///   then its image lock, then a shared creator's, character's, studio's or
///   network's lock, which is the entry lock of that entity. Nothing takes
///   an earlier one under a later one, so no two holders wait on each other.
/// </remarks>
public sealed class MetadataEntryLocks
{
    #region Fields

    private readonly Lock _gate = new();

    private readonly Dictionary<MetadataGuid, EntryLock> _locks = [];

    private readonly Dictionary<MetadataGuid, EntryLock> _imageLocks = [];

    private readonly Dictionary<MetadataGuid, Updating> _updating = [];

    #endregion

    #region Locking

    /// <summary>
    ///   Wait for an entry's lock and take it.
    /// </summary>
    /// <param name="entry">The series, movie or collection, which also names its source.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>A handle that releases the lock when disposed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled while waiting.</exception>
    public Task<IDisposable> Acquire(MetadataGuid entry, CancellationToken cancellationToken = default)
        => Acquire(_locks, entry, cancellationToken);

    /// <summary>
    ///   Wait for an entry's image lock and take it.
    /// </summary>
    /// <remarks>
    ///   Taken by the image job and, after the entry's lock, by a purge, so
    ///   images are never linked to what a purge removes. A refresh does not
    ///   take it, so a refresh never waits for images.
    /// </remarks>
    /// <param name="entry">The series, movie or collection, which also names its source.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>A handle that releases the lock when disposed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled while waiting.</exception>
    public Task<IDisposable> AcquireImages(MetadataGuid entry, CancellationToken cancellationToken = default)
        => Acquire(_imageLocks, entry, cancellationToken);

    /// <summary>
    ///   Wait for one of an entry's locks and take it.
    /// </summary>
    /// <param name="locks">The kind of lock, by entry.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>A handle that releases the lock when disposed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled while waiting.</exception>
    private async Task<IDisposable> Acquire(Dictionary<MetadataGuid, EntryLock> locks, MetadataGuid entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        EntryLock entryLock;
        lock (_gate)
        {
            if (!locks.TryGetValue(entry, out entryLock!))
                locks[entry] = entryLock = new();
            entryLock.Users++;
        }

        try
        {
            await entryLock.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Leave(locks, entry, entryLock);
            throw;
        }

        return new Handle(this, locks, entry, entryLock);
    }

    /// <summary>
    ///   Whether somebody holds or waits for an entry's lock.
    /// </summary>
    /// <param name="entry">The series, movie or collection.</param>
    /// <returns><see langword="true"/> when the lock is in use.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    public bool IsInUse(MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
            return _locks.ContainsKey(entry);
    }

    /// <summary>
    ///   Stops using one of an entry's locks, dropping it once nobody else
    ///   does.
    /// </summary>
    /// <param name="locks">The kind of lock, by entry.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="entryLock">Its lock.</param>
    private void Leave(Dictionary<MetadataGuid, EntryLock> locks, MetadataGuid entry, EntryLock entryLock)
    {
        lock (_gate)
        {
            if (--entryLock.Users is 0)
                locks.Remove(entry);
        }
    }

    #endregion

    #region Updates

    /// <summary>
    ///   Mark an entry as being refreshed or purged, which readers may wait
    ///   out, until the handle is disposed.
    /// </summary>
    /// <remarks>
    ///   Taken under the entry's lock by the refresh and purge jobs, and not
    ///   by the image job, so a reader waits for the entry's data and not
    ///   for its images.
    /// </remarks>
    /// <param name="entry">The series, movie or collection.</param>
    /// <returns>A handle that ends the update when disposed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    public IDisposable MarkUpdating(MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            if (!_updating.TryGetValue(entry, out var updating))
                _updating[entry] = updating = new();
            updating.Count++;
            return new UpdateHandle(this, entry, updating);
        }
    }

    /// <summary>
    ///   Whether an entry is being refreshed or purged.
    /// </summary>
    /// <param name="entry">The series, movie or collection.</param>
    /// <returns><see langword="true"/> while it is.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    public bool IsUpdating(MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
            return _updating.ContainsKey(entry);
    }

    /// <summary>
    ///   Wait for an entry's refresh or purge to end, if one is running.
    /// </summary>
    /// <param name="entry">The series, movie or collection.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns><see langword="true"/> when there was one to wait for.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled while waiting.</exception>
    public async Task<bool> WaitForUpdate(MetadataGuid entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Task done;
        lock (_gate)
        {
            if (!_updating.TryGetValue(entry, out var updating))
                return false;

            done = updating.Done.Task;
        }

        await done.WaitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    ///   Ends one of an entry's updates, and lets the waiters go once none is
    ///   left.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="updating">Its update.</param>
    private void EndUpdate(MetadataGuid entry, Updating updating)
    {
        lock (_gate)
        {
            if (--updating.Count is not 0)
                return;

            _updating.Remove(entry);
        }

        updating.Done.TrySetResult();
    }

    #endregion

    #region Nested Types

    /// <summary>
    ///   One entry's running updates, and what their end is waited on by.
    /// </summary>
    private sealed class Updating
    {
        public TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Count { get; set; }
    }

    /// <summary>
    ///   Ends an entry's update once, however often it is disposed.
    /// </summary>
    /// <param name="owner">The locks.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="updating">Its update.</param>
    private sealed class UpdateHandle(MetadataEntryLocks owner, MetadataGuid entry, Updating updating) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) is 0)
                owner.EndUpdate(entry, updating);
        }
    }

    /// <summary>
    ///   One entry's lock, and how many hold or wait for it.
    /// </summary>
    private sealed class EntryLock
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int Users { get; set; }
    }

    /// <summary>
    ///   Releases one of an entry's locks once, however often it is disposed.
    /// </summary>
    /// <param name="owner">The locks.</param>
    /// <param name="locks">The kind of lock, by entry.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="entryLock">Its lock.</param>
    private sealed class Handle(MetadataEntryLocks owner, Dictionary<MetadataGuid, EntryLock> locks, MetadataGuid entry, EntryLock entryLock) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) is not 0)
                return;

            entryLock.Semaphore.Release();
            owner.Leave(locks, entry, entryLock);
        }
    }

    #endregion
}
