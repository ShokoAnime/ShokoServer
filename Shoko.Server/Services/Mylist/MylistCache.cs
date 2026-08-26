using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Plugin;

namespace Shoko.Server.Services.Mylist;

/// <summary>
/// A local, queryable cache of the AniDB MyList. The cache is persisted to
/// disk so it survives restarts, and is kept in sync with upstream by
/// <see cref="MylistService"/> whenever AniDB accepts an add, update, or
/// remove. It is used to short-circuit MyList operations that would otherwise
/// require a UDP round-trip when the entry is already in the desired state.
/// </summary>
public sealed class MylistCache : IDisposable
{
    /// <summary>
    /// How long to let changes accumulate before writing the cache out. A sync
    /// can touch thousands of entries, and each one used to serialise and
    /// rewrite the whole document while holding the write lock.
    /// </summary>
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);

    private readonly ILogger<MylistCache> _logger;
    private readonly IApplicationPaths _applicationPaths;

    private readonly ConcurrentDictionary<ulong, MylistEntry> _byLid = new();
    private readonly ConcurrentDictionary<int, MylistEntry> _byFileID = new();
    private readonly ConcurrentDictionary<(string ed2k, long size), MylistEntry> _byEd2k = new();
    private readonly ConcurrentDictionary<int, MylistEntry> _byEpisodeID = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private bool _loaded;
    private DateTime? _lastFetchedAt;
    private bool _dirty;
    private readonly Timer _flushTimer;

    public MylistCache(ILogger<MylistCache> logger, IApplicationPaths applicationPaths)
    {
        _logger = logger;
        _applicationPaths = applicationPaths;
        _flushTimer = new Timer(_ => Flush(), null, FlushInterval, FlushInterval);
    }

    /// <summary>
    /// Writes pending changes out. Runs on a timer and at shutdown, and is a
    /// no-op when nothing has changed.
    /// </summary>
    public void Flush()
    {
        List<MylistEntry> snapshot;
        DateTime? lastFetchedAt;
        _lock.EnterWriteLock();
        try
        {
            if (!_dirty) return;
            _dirty = false;
            snapshot = GetAllUnlocked();
            lastFetchedAt = _lastFetchedAt;
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        // only the snapshot needs the lock; serialising and writing do not
        Persist(snapshot, lastFetchedAt);
    }

    public void Dispose()
    {
        _flushTimer.Dispose();
        Flush();
    }

    private string CachePath => Path.Combine(_applicationPaths.DataPath, "MyList", "mylist.json.gz");

    /// <summary>
    /// The backup older builds wrote before backups moved into <c>Backups/</c>
    /// and gained rotation. It was only ever written, never read back, and its
    /// entries predate fields the sync now compares on, so it is deleted rather
    /// than loaded.
    /// </summary>
    private string LegacyBackupPath => Path.Combine(_applicationPaths.DataPath, "MyList", "mylist.json");

    /// <summary>
    /// When the cache was last replaced by a full fetch from AniDB, or
    /// <c>null</c> if it has never been fetched in this session.
    /// </summary>
    public DateTime? LastFetchedAt
    {
        get
        {
            EnsureLoaded();
            _lock.EnterReadLock();
            try
            {
                return _lastFetchedAt;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// All cached entries.
    /// </summary>
    public IReadOnlyList<MylistEntry> GetAll()
    {
        EnsureLoaded();
        _lock.EnterReadLock();
        try
        {
            return GetAllUnlocked();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Get the cached entry with the given list ID (lid), if present.
    /// </summary>
    public MylistEntry? GetByLid(ulong mylistID)
    {
        EnsureLoaded();
        _lock.EnterReadLock();
        try
        {
            return _byLid.GetValueOrDefault(mylistID);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Get the cached entry for the file with the given file ID (fid), if
    /// present.
    /// </summary>
    public MylistEntry? GetByFileID(int fileID)
    {
        EnsureLoaded();
        _lock.EnterReadLock();
        try
        {
            return _byFileID.GetValueOrDefault(fileID);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Get the cached entry for the file with the given ED2K hash and size,
    /// if present.
    /// </summary>
    public MylistEntry? GetByEd2k(string ed2k, long size)
    {
        EnsureLoaded();
        _lock.EnterReadLock();
        try
        {
            return _byEd2k.GetValueOrDefault((ed2k, size));
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Get the cached entry for the episode with the given AniDB episode ID
    /// (eid), if present.
    /// </summary>
    public MylistEntry? GetByEpisodeID(int episodeID)
    {
        EnsureLoaded();
        _lock.EnterReadLock();
        try
        {
            return _byEpisodeID.GetValueOrDefault(episodeID);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Add or update a cached entry, persisting the cache to disk. Entries
    /// without a list ID are still cached when they carry a file ID, ED2K
    /// hash and size, or episode ID.
    /// </summary>
    public void Upsert(MylistEntry entry)
    {
        EnsureLoaded();
        _lock.EnterWriteLock();
        try
        {
            if (entry.MylistID is 0 && entry.FileID is 0 && entry.EpisodeID is 0 && entry.ED2K is null)
            {
                _logger.LogWarning("Refusing to cache a MyList entry without any identification");
                return;
            }

            IndexUnlocked(entry);
            _dirty = true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Remove the cached entry with the given list ID (lid), persisting the
    /// cache to disk.
    /// </summary>
    public void Remove(ulong mylistID)
    {
        EnsureLoaded();
        _lock.EnterWriteLock();
        try
        {
            if (!_byLid.TryRemove(mylistID, out var entry)) return;
            RemoveUnlocked(entry);
            _dirty = true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Remove the given cached entry from every index it is keyed under,
    /// persisting the cache to disk. Entries without a list ID are removed
    /// by their file ID, ED2K hash and size, or episode ID.
    /// </summary>
    public void Remove(MylistEntry entry)
    {
        EnsureLoaded();
        _lock.EnterWriteLock();
        try
        {
            if (entry.MylistID is not 0) _byLid.TryRemove(entry.MylistID, out _);
            RemoveUnlocked(entry);
            _dirty = true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Replace the entire cache with the given entries, persisting to disk.
    /// The locally enriched ED2K hash and size are carried forward from the
    /// previous entries when the new entries are missing them, in case the
    /// files are no longer present locally to enrich from.
    /// </summary>
    public IReadOnlyList<MylistEntry> ReplaceAll(IEnumerable<MylistEntry> entries)
    {
        EnsureLoaded();
        List<MylistEntry> snapshot;
        List<MylistEntry> entriesList;
        DateTime? lastFetchedAt;
        _lock.EnterWriteLock();
        try
        {
            // carry forward what a fetch cannot rediscover on its own — the locally
            // enriched ED2K and size, and whether the entry is generic — from the
            // previous entries, matched by list ID, file ID, or episode ID
            entriesList = entries.ToList();
            for (var i = 0; i < entriesList.Count; i++)
            {
                var entry = entriesList[i];
                var needsIdentity = entry.ED2K is null || entry.Size is not > 0;
                var needsGeneric = entry.IsGeneric is null;
                if (!needsIdentity && !needsGeneric) continue;

                var previous = entry.MylistID is not 0 && _byLid.TryGetValue(entry.MylistID, out var byLid) ? byLid
                    : entry.FileID is not 0 && _byFileID.TryGetValue(entry.FileID, out var byFileID) ? byFileID
                    : entry.EpisodeID is not 0 && _byEpisodeID.TryGetValue(entry.EpisodeID, out var byEpisodeID) ? byEpisodeID
                    : null;
                if (previous is null) continue;

                if (needsIdentity && previous is { ED2K: not null, Size: > 0 })
                    entry = entry with { ED2K = previous.ED2K, Size = previous.Size };
                // a fetch taken while the generics index was unavailable knows
                // nothing about generic-ness; keep what we previously established
                // rather than downgrading it to unknown
                if (needsGeneric && previous.IsGeneric is not null)
                    entry = entry with { IsGeneric = previous.IsGeneric };
                entriesList[i] = entry;
            }

            _byLid.Clear();
            _byFileID.Clear();
            _byEd2k.Clear();
            _byEpisodeID.Clear();
            foreach (var entry in entriesList)
                IndexUnlocked(entry);

            _loaded = true;
            _lastFetchedAt = DateTime.UtcNow;
            _dirty = false;
            snapshot = GetAllUnlocked();
            lastFetchedAt = _lastFetchedAt;
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        // a full replace is rare and carries the fetch stamp, so it is worth
        // writing straight away rather than waiting on the timer
        Persist(snapshot, lastFetchedAt);

        // the carried-forward ED2K, size and generic-ness live on these, not on
        // what the caller handed in, so the caller needs these back
        return entriesList;
    }

    /// <summary>
    /// Whether the cached entry already matches the state that an add request
    /// would send, mirroring <c>ApplyAddData</c>.
    /// </summary>
    public static bool IsInDesiredState(MylistEntry entry, MylistAddData? data, DateTime? fallbackWatchedDate = null)
        => IsInDesiredState(entry, data is { } addData ? (MylistUpdateData)addData : null, fallbackWatchedDate);

    /// <summary>
    /// Whether the cached entry already matches the state that an update
    /// request would send, mirroring <c>ApplyUpdateData</c>.
    /// </summary>
    public static bool IsInDesiredState(MylistEntry entry, MylistUpdateData? data, DateTime? fallbackWatchedDate = null)
    {
        data ??= new MylistUpdateData();
        if (data.State is not null && entry.State != data.State) return false;
        // an unwritable file state is dropped from the request, so comparing it
        // here would keep reporting a difference no request could ever close
        if (data.FileState is { IsWritable: true } fileState && entry.FileState != fileState) return false;

        if (data.IsViewed.HasValue || data.ViewedAt.HasValue || fallbackWatchedDate.HasValue)
        {
            var desiredViewedAt = data.ViewedAt ?? fallbackWatchedDate;
            var desiredIsViewed = data.IsViewed ?? desiredViewedAt.HasValue;
            if (entry.IsViewed != desiredIsViewed) return false;
            if (desiredIsViewed && !SameSecond(entry.ViewedAt, desiredViewedAt)) return false;
        }

        if (data.Storage is not null && entry.Storage != data.Storage) return false;
        if (data.Source is not null && entry.Source != data.Source) return false;
        if (data.Other is not null && entry.Other != data.Other) return false;
        return true;
    }

    private static bool SameSecond(DateTime? a, DateTime? b)
    {
        if (a is null || b is null) return a == b;
        return Math.Abs((a.Value - b.Value).TotalSeconds) < 1;
    }

    /// <summary>
    /// Every cached entry, across all indexes. Must be called under the lock.
    /// </summary>
    private List<MylistEntry> GetAllUnlocked()
        // the indexes share entry instances, so reference identity dedups them
        // without hashing all 18 fields of every record
        => _byLid.Values
            .Concat(_byFileID.Values)
            .Concat(_byEd2k.Values)
            .Concat(_byEpisodeID.Values)
            .Distinct(ReferenceEqualityComparer.Instance)
            .Cast<MylistEntry>()
            .ToList();

    /// <summary>
    /// Index the entry under every identifier it carries. Must be called
    /// under the lock.
    /// </summary>
    private void IndexUnlocked(MylistEntry entry)
    {
        if (entry.MylistID is 0 && entry.FileID is 0 && entry.EpisodeID is 0 && entry.ED2K is null) return;
        if (entry.MylistID is not 0) _byLid[entry.MylistID] = entry;
        if (entry.FileID is not 0) _byFileID[entry.FileID] = entry;
        if (entry.ED2K is not null && entry.Size is > 0) _byEd2k[(entry.ED2K, entry.Size.Value)] = entry;
        // only generic entries belong in the episode index. AniDB reports an
        // episode ID for real files too, so indexing on that alone lets a real
        // file answer a lookup for the episode's generic entry — and the caller
        // then removes, updates or skips the wrong one
        if (entry.IsGeneric is true && entry.EpisodeID is not 0) _byEpisodeID[entry.EpisodeID] = entry;
    }

    /// <summary>
    /// Drop the entry from every index except the list ID index. Must be
    /// called under the lock.
    /// </summary>
    private void RemoveUnlocked(MylistEntry entry)
    {
        if (entry.FileID is not 0) _byFileID.TryRemove(entry.FileID, out _);
        if (entry.ED2K is not null && entry.Size is > 0) _byEd2k.TryRemove((entry.ED2K, entry.Size.Value), out _);
        if (entry.IsGeneric is true && entry.EpisodeID is not 0) _byEpisodeID.TryRemove(entry.EpisodeID, out _);
    }

    private static string ReadCompressed(string path)
    {
        using var fileStream = File.OpenRead(path);
        using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Removes the legacy backup without reading it. Seeding the cache from
    /// entries that are missing fields would leave them looking complete when
    /// they are not; an empty cache just fetches a fresh copy instead.
    /// </summary>
    private void DiscardLegacyBackup()
    {
        try
        {
            if (!File.Exists(LegacyBackupPath)) return;

            File.Delete(LegacyBackupPath);
            _logger.LogInformation("Removed the legacy MyList backup; it predates the current entry shape. (Path={Path})", LegacyBackupPath);
        }
        catch (Exception ex)
        {
            // it is never read either way, so failing to remove it changes nothing
            _logger.LogWarning(ex, "Failed to remove the legacy MyList backup. (Path={Path})", LegacyBackupPath);
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        _lock.EnterWriteLock();
        try
        {
            if (_loaded) return;
            DiscardLegacyBackup();

            try
            {
                if (File.Exists(CachePath))
                {
                    var file = JsonConvert.DeserializeObject<CacheFile>(ReadCompressed(CachePath));
                    _lastFetchedAt = file?.LastFetchedAt;

                    var loaded = 0;
                    foreach (var entry in file?.Entries ?? [])
                    {
                        IndexUnlocked(entry);
                        loaded++;
                    }

                    _logger.LogInformation("Loaded {Count} MyList entries from cache", loaded);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load MyList cache from {Path}", CachePath);
            }

            _loaded = true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void Persist(List<MylistEntry> entries, DateTime? lastFetchedAt)
    {
        try
        {
            var directory = Path.GetDirectoryName(CachePath);
            if (directory is not null) Directory.CreateDirectory(directory);
            // compact: this is a machine-read file that runs to tens of megabytes indented
            var serialized = JsonConvert.SerializeObject(new CacheFile { LastFetchedAt = lastFetchedAt, Entries = entries }, Formatting.None);
            var tempPath = CachePath + ".tmp";
            using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal))
            using (var writer = new StreamWriter(gzipStream))
                writer.Write(serialized);
            File.Move(tempPath, CachePath, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist MyList cache to {Path}", CachePath);
        }
    }

    /// <summary>
    /// The on-disk shape of the cache. The fetch stamp is persisted alongside
    /// the entries so the cache does not read as never-fetched after a
    /// restart.
    /// </summary>
    private sealed class CacheFile
    {
        public DateTime? LastFetchedAt { get; set; }

        public List<MylistEntry> Entries { get; set; } = [];
    }
}
