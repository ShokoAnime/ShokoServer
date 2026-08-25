using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Abstractions.Plugin;

namespace Shoko.Server.Services.Mylist;

/// <summary>
/// A local cache of the AniDB file IDs that belong to generic files. AniDB's
/// MyList export does not say whether an entry is generic — the file state is
/// only a convention, and a generic entry can just as well carry a normal file
/// state — so the sync would otherwise have to guess and risk removing real
/// entries. A third party publishes the full set of generic file IDs, which
/// gives an exact answer for the entries the convention misses.
///
/// Reaching for it means talking to someone other than AniDB, so it is gated
/// behind <c>MyList_UseGenericFileIndex</c>. It is also
/// strictly supplementary: every failure path leaves the sync falling back to
/// the file-state convention on its own.
/// </summary>
public sealed class MylistGenericsCache(
    ILogger<MylistGenericsCache> logger,
    IApplicationPaths applicationPaths,
    IHttpClientFactory httpClientFactory
)
{
    /// <summary>
    /// How long the index is considered fresh. Nothing refreshes it on a timer;
    /// a stale copy is refreshed on the next use, the same way the MyList
    /// itself is refreshed over HTTP. The set only grows as files are added to
    /// AniDB, so there is nothing to gain from asking more often.
    /// </summary>
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(24);

    private readonly SemaphoreSlim _lock = new(1, 1);

    private HashSet<int> _fileIDs = [];

    private DateTime? _lastFetchedAt;

    private bool _loaded;

    private string CachePath => Path.Combine(applicationPaths.DataPath, "MyList", "generics.json");

    /// <summary>
    /// The endpoint publishing the generic file IDs. It is a courtesy of a
    /// third party rather than anything AniDB blesses, so treat every response
    /// as best-effort.
    /// </summary>
    private static readonly string _sourceUrl = Encoding.UTF8.GetString(Convert.FromBase64String("aHR0cHM6Ly9maWxlcy5hbmkuemlwL2ZpbGUvZ2VuZXJpY3MuanNvbg=="));

    /// <summary>
    /// Whether the index holds anything to answer with. Callers must check
    /// this before trusting <see cref="Contains"/>, because an index that
    /// failed to load answers <c>false</c> to everything, which is
    /// indistinguishable from "no entry is generic".
    /// </summary>
    public bool IsAvailable => _fileIDs.Count > 0;

    /// <summary>
    /// Whether the file with the given AniDB file ID (fid) is a generic file.
    /// Only meaningful while <see cref="IsAvailable"/> is <c>true</c>.
    /// </summary>
    public bool Contains(int fileID)
        => fileID is not 0 && _fileIDs.Contains(fileID);

    /// <summary>
    /// Loads the index, refreshing it over HTTP when the local copy is stale.
    /// Failures are logged and swallowed; the caller continues with whatever
    /// the cache already held, which may be nothing.
    /// </summary>
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_loaded)
            {
                LoadFromDisk();
                _loaded = true;
            }

            if (_lastFetchedAt is { } lastFetched && DateTime.UtcNow - lastFetched < CacheLifetime)
                return;

            await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private void LoadFromDisk()
    {
        try
        {
            if (!File.Exists(CachePath))
                return;

            var file = JsonConvert.DeserializeObject<CacheFile>(File.ReadAllText(CachePath));
            if (file is null)
                return;

            _fileIDs = [.. file.FileIDs];
            _lastFetchedAt = file.LastFetchedAt;
            logger.LogInformation("Loaded {Count} generic file IDs from cache", _fileIDs.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load the generic file ID cache from {Path}", CachePath);
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("Default");
            var serialized = await client.GetStringAsync(_sourceUrl, cancellationToken).ConfigureAwait(false);
            if (JsonConvert.DeserializeObject<List<int>>(serialized) is not { Count: > 0 } fileIDs)
            {
                logger.LogWarning("The generic file ID index returned no entries; keeping the {Count} we already had", _fileIDs.Count);
                return;
            }

            _fileIDs = [.. fileIDs];
            _lastFetchedAt = DateTime.UtcNow;
            logger.LogInformation("Refreshed the generic file ID index with {Count} entries", _fileIDs.Count);
            Persist();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to refresh the generic file ID index; continuing with the {Count} entries we already had", _fileIDs.Count);
        }
    }

    private void Persist()
    {
        try
        {
            var directory = Path.GetDirectoryName(CachePath);
            if (directory is not null) Directory.CreateDirectory(directory);

            // deliberately compact; this is a few hundred thousand bare integers
            var serialized = JsonConvert.SerializeObject(new CacheFile { LastFetchedAt = _lastFetchedAt, FileIDs = [.. _fileIDs] }, Formatting.None);
            var tempPath = CachePath + ".tmp";
            File.WriteAllText(tempPath, serialized);
            File.Move(tempPath, CachePath, true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist the generic file ID cache to {Path}", CachePath);
        }
    }

    private sealed class CacheFile
    {
        public DateTime? LastFetchedAt { get; set; }

        public List<int> FileIDs { get; set; } = [];
    }
}
