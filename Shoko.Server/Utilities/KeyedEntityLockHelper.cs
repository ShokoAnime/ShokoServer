using System;
using System.Threading;
using System.Threading.Tasks;
using AsyncKeyedLock;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Enums;

#nullable enable
namespace Shoko.Server.Utilities;

/// <summary>
/// Keyed async locks for entity-scoped operations (metadata, images, purge), using <see cref="AsyncKeyedLocker{T}"/>.
/// </summary>
public sealed class KeyedEntityLockHelper
{
    private readonly ILogger _logger;
    private readonly AsyncKeyedLocker<string> _locker = new(o =>
    {
        o.PoolSize = 50;
        o.MaxCount = 1;
    });

    public KeyedEntityLockHelper(ILogger logger)
    {
        _logger = logger;
    }

    public static string BuildKey(DataEntityType entityType, int id, string metadataKey)
        => $"{entityType.ToString().ToLowerInvariant()}-{metadataKey}:{id}";

    public async Task<IDisposable> GetLockForEntityAsync(DataEntityType entityType, int id, string metadataKey, string reason, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(entityType, id, metadataKey);
        var startedAt = DateTime.Now;
        _logger.LogDebug("Acquiring lock '{MetadataKey}' for {EntityType} {Id}. (Reason: {Reason})", metadataKey, entityType, id, reason);

        var releaser = await _locker.LockOrNullAsync(key, TimeSpan.FromMilliseconds(500), cancellationToken, false).ConfigureAwait(false);
        if (releaser is null)
        {
            _logger.LogDebug("Waiting for lock '{MetadataKey}' for {EntityType} {Id}. (Reason: {Reason})", metadataKey, entityType, id, reason);
            releaser = await _locker.LockAsync(key, cancellationToken, false).ConfigureAwait(false);
            var deltaTime = DateTime.Now - startedAt;
            _logger.LogDebug("Waited {Waited} for lock '{MetadataKey}' for {EntityType} {Id}. (Reason: {Reason})", deltaTime, metadataKey, entityType, id, reason);
        }

        _logger.LogDebug("Acquired lock '{MetadataKey}' for {EntityType} {Id}. (Reason: {Reason})", metadataKey, entityType, id, reason);

        var released = false;
        return new DisposableAction(() =>
        {
            if (released) return;
            released = true;
            releaser.Dispose();
            var deltaTime = DateTime.Now - startedAt;
            _logger.LogDebug("Released lock '{MetadataKey}' for {EntityType} {Id} after {Run}. (Reason: {Reason})", metadataKey, entityType, id, deltaTime, reason);
        });
    }

    public bool WaitIfEntityLocked(DataEntityType entityType, int id, string metadataKey)
    {
        var key = BuildKey(entityType, id, metadataKey);
        if (!_locker.IsInUse(key))
            return false;

        using (_locker.Lock(key))
            return true;
    }

    public async Task<bool> WaitIfEntityLockedAsync(DataEntityType entityType, int id, string metadataKey)
    {
        var key = BuildKey(entityType, id, metadataKey);
        if (!_locker.IsInUse(key))
            return false;

        using (await _locker.LockAsync(key, false).ConfigureAwait(false))
            return true;
    }

    public bool IsEntityLocked(DataEntityType entityType, int id, string metadataKey)
    {
        var key = BuildKey(entityType, id, metadataKey);
        return _locker.IsInUse(key);
    }

    private sealed class DisposableAction(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}
