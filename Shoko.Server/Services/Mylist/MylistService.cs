using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Iesi.Collections.Generic;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.User.Enums;
using Shoko.Abstractions.User.Services;
using Shoko.Abstractions.Video;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Release;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Settings;

namespace Shoko.Server.Services.Mylist;

/// <summary>
/// Service for interacting with the AniDB MyList. All MyList operations,
/// whether immediate or scheduled through the job queue, should be routed
/// through this service.
/// </summary>
public class MylistService(
    ILogger<MylistService> logger,
    IRequestFactory requestFactory,
    IQueueScheduler scheduler,
    ISettingsProvider settingsProvider,
    IApplicationPaths applicationPaths,
    IUserDataService userDataService,
    MylistCache mylistCache,
    MylistGenericsCache genericsCache,
    JMMUserRepository users,
    VideoLocalRepository videoLocals,
    VideoLocal_UserRepository videoLocalUsers,
    AnimeEpisodeRepository animeEpisodes,
    AnimeEpisode_UserRepository animeEpisodeUsers,
    AniDB_EpisodeRepository anidbEpisodes,
    StoredReleaseInfoRepository storedReleaseInfos,
    AnimeSeriesService seriesService
) : IMylistService
{
    /// <summary>
    /// How long the locally cached MyList is considered fresh enough to serve
    /// without going back to AniDB over HTTP. Deliberately independent of
    /// <c>MyList_UpdateFrequency</c>, which schedules the sync rather than
    /// bounding the cache; callers that need a guaranteed-current entry pass
    /// <see cref="MylistFetchMode.IgnoreTimeCheck"/> instead.
    /// </summary>
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(12);

    private readonly ILogger<MylistService> _logger = logger;

    /// <summary>
    /// Guards <see cref="SyncAsync(MylistSyncOptions?, CancellationToken)"/> and its
    /// scoped overload against overlapping runs. The queue job
    /// is already <c>[DisallowConcurrentExecution]</c>, but the method is on the
    /// plugin-facing contract and can be called without going through it.
    /// </summary>
    internal readonly SemaphoreSlim _syncLock = new(1, 1);

    /// <summary>
    /// Collapses concurrent full MyList downloads into one. The queue's
    /// concurrency groups already serialise each family of jobs against itself,
    /// so this only catches the overlaps they cannot: a sync against an entry
    /// job, and anything against a direct call from a plugin.
    ///
    /// Unlike <see cref="_syncLock"/> this one is waited on rather than tried,
    /// because a caller here needs the entries: it queues for the winner's
    /// result instead of giving up.
    /// </summary>
    private readonly SemaphoreSlim _fetchLock = new(1, 1);

    public MylistFetchMode FetchMode
    {
        get => settingsProvider.GetSettings().AniDb.MyList_FetchMode;
        set
        {
            if (value is MylistFetchMode.Auto or MylistFetchMode.None)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Fetch mode cannot be Auto or None");
            var settings = settingsProvider.GetSettings();
            settings.AniDb.MyList_FetchMode = value;
            settingsProvider.SaveSettings();
        }
    }

    /// <summary>
    /// The flags that pick a transport, as opposed to the modifier flags that
    /// only adjust how one behaves.
    /// </summary>
    private const MylistFetchMode TransportFlags = MylistFetchMode.Http | MylistFetchMode.Udp | MylistFetchMode.Cache;

    /// <summary>
    /// Resolves <see cref="MylistFetchMode.Auto"/> to the configured mode, and
    /// fills the configured transports in when the caller passed modifier flags
    /// only. <see cref="MylistFetchMode.IgnoreTimeCheck"/> on its own carries no
    /// transport, so taking it at face value would fetch nothing at all.
    /// <see cref="MylistFetchMode.None"/> stays as it is; it means do nothing.
    /// </summary>
    internal MylistFetchMode ResolveFetchMode(MylistFetchMode fetchMode)
    {
        if (fetchMode is MylistFetchMode.Auto)
            return settingsProvider.GetSettings().AniDb.MyList_FetchMode;

        if (fetchMode is MylistFetchMode.None || (fetchMode & TransportFlags) is not MylistFetchMode.None)
            return fetchMode;

        return settingsProvider.GetSettings().AniDb.MyList_FetchMode | fetchMode;
    }

    private MylistReadStates ResolveReadStates(MylistReadStates readStates)
    {
        if (readStates is not MylistReadStates.Auto)
            return readStates;

        var settings = settingsProvider.GetSettings();
        var resolved = MylistReadStates.None;
        if (settings.AniDb.MyList_ReadWatched) resolved |= MylistReadStates.Watched;
        if (settings.AniDb.MyList_ReadUnwatched) resolved |= MylistReadStates.Unwatched;
        return resolved;
    }

    #region Fetch

    public async Task<IReadOnlyList<MylistEntry>> GetEntriesAsync(MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MylistFetchMode.None)
            return [];

        // serve the cache when allowed and fresh, or when the time check is ignored
        if (fetchMode.HasFlag(MylistFetchMode.Cache))
        {
            var cached = mylistCache.GetAll();
            if (cached.Count > 0 && (fetchMode.HasFlag(MylistFetchMode.IgnoreTimeCheck) || IsCacheFresh()))
                return cached;
        }

        if (fetchMode.HasFlag(MylistFetchMode.Http))
        {
            try
            {
                return await FetchMylistAsync(cancellationToken);
            }
            catch (AnidbHttpBannedException ex)
            {
                _logger.LogWarning("Got an AniDB HTTP ban while fetching the MyList. Expires: {ExpiresAt}", ex.ExpiresAt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch the MyList over HTTP");
            }
        }

        return mylistCache.GetAll();
    }

    public Task<MylistEntry?> GetEntryAsync(ulong listID, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default)
        => GetEntryInternalAsync(fetchMode, () => mylistCache.GetByLid(listID), r => r.MylistID = listID, cancellationToken: cancellationToken);

    public Task<MylistEntry?> GetEntryAsync(string ed2k, long fileSize, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default)
        => GetEntryInternalAsync(fetchMode, () => mylistCache.GetByEd2k(ed2k, fileSize), r =>
        {
            r.ED2K = ed2k;
            r.Size = fileSize;
        }, entry => entry with { ED2K = ed2k, Size = fileSize, IsGeneric = false }, cancellationToken);

    public Task<MylistEntry?> GetEntryAsync(int fileID, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default)
        => GetEntryInternalAsync(fetchMode, () => mylistCache.GetByFileID(fileID), r => r.FileID = (ulong)fileID, cancellationToken: cancellationToken);

    public Task<MylistEntry?> GetEntryAsync(int animeID, EpisodeType episodeType, int episodeNumber, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        var anidbEpisode = anidbEpisodes.GetByAnimeIDAndEpisodeTypeNumber(animeID, episodeType, episodeNumber).FirstOrDefault();
        return GetEntryInternalAsync(fetchMode, () => anidbEpisode is null ? null : mylistCache.GetByEpisodeID(anidbEpisode.EpisodeID), r =>
        {
            r.AnimeID = animeID;
            r.EpisodeType = episodeType;
            r.EpisodeNumber = episodeNumber;
        }, entry => anidbEpisode is null ? entry : entry with { AnimeID = animeID, EpisodeID = anidbEpisode.EpisodeID, IsGeneric = true }, cancellationToken);
    }

    public async Task<IReadOnlyList<MylistEntry>> GetEntriesForVideoAsync(IVideo video, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        var vid = videoLocals.GetByID(video.ID);
        if (vid is null)
            return [];

        var entries = new List<MylistEntry>();
        if (vid.ReleaseInfo is { } releaseInfo && (releaseInfo.ReleaseURI?.StartsWith(AnidbReleaseProvider.ReleasePrefix) ?? false))
        {
            if (await GetEntryAsync(vid.Hash, vid.FileSize, fetchMode, cancellationToken) is { } entry)
                entries.Add(entry);
        }
        else
        {
            // we have a manual link, so the entries are the generic entries of each linked episode
            foreach (var episode in vid.EpisodeCrossReferences.Select(xref => xref.AniDBEpisode).WhereNotNull())
            {
                if (await GetEntryAsync(episode.AnimeID, episode.EpisodeType, episode.EpisodeNumber, fetchMode, cancellationToken) is { } entry)
                    entries.Add(entry);
            }
        }

        return entries;
    }

    #region Fetch | Private

    /// <summary>
    /// The shared resolution logic for the per-entry getters. HTTP is tried
    /// first, gated by the time check unless ignored, then the cache, then
    /// UDP only when HTTP was not used successfully.
    /// </summary>
    private async Task<MylistEntry?> GetEntryInternalAsync(
        MylistFetchMode fetchMode,
        Func<MylistEntry?> cacheLookup,
        Action<RequestGetMylist> configureRequest,
        Func<MylistEntry, MylistEntry>? enrichEntry = null,
        CancellationToken cancellationToken = default
    )
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MylistFetchMode.None)
            return null;

        // 1. HTTP — gated by the time check unless ignored
        var httpSucceeded = false;
        if (fetchMode.HasFlag(MylistFetchMode.Http) && (fetchMode.HasFlag(MylistFetchMode.IgnoreTimeCheck) || !IsCacheFresh()))
        {
            try
            {
                await FetchMylistAsync(cancellationToken);
                httpSucceeded = true;
            }
            catch (AnidbHttpBannedException ex)
            {
                _logger.LogWarning("Got an AniDB HTTP ban while fetching the MyList. Expires: {ExpiresAt}", ex.ExpiresAt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch the MyList over HTTP");
            }
        }

        // 2. Cache
        if (fetchMode.HasFlag(MylistFetchMode.Cache) && cacheLookup() is { } cached)
            return cached;

        // 3. UDP — only when HTTP was not used successfully
        if (fetchMode.HasFlag(MylistFetchMode.Udp) && !httpSucceeded)
        {
            try
            {
                var request = requestFactory.Create(configureRequest);
                var response = request.Send();
                if (response.Response is { } entry)
                {
                    if (enrichEntry is not null) entry = enrichEntry(entry);
                    mylistCache.Upsert(entry);
                    return entry;
                }
            }
            catch (AniDBBannedException)
            {
                _logger.LogWarning("Got an AniDB UDP ban while fetching a MyList entry");
            }
            catch (UnexpectedUDPResponseException ex)
            {
                _logger.LogWarning("AniDB did not return a MyList entry: {Code}", ex.ReturnCode);
            }
        }

        return null;
    }

    /// <summary>
    /// Fetches the full MyList over HTTP, replacing the local cache.
    /// Throws when AniDB does not return a successful response.
    /// </summary>
    private async Task<IReadOnlyList<MylistEntry>> FetchMylistAsync(CancellationToken cancellationToken)
    {
        // one export at a time. The entry jobs share a concurrency group and so
        // are already serialised against each other, but a sync job is in a
        // different one, and a plugin calling in touches no queue at all — and
        // the whole MyList comes down on every call that gets this far
        var fetchedBefore = mylistCache.LastFetchedAt;
        await _fetchLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // somebody fetched while we queued, so theirs is as good as ours
            // would have been
            if (mylistCache.LastFetchedAt is { } fetchedAfter && fetchedAfter != fetchedBefore)
                return mylistCache.GetAll();

            var settings = settingsProvider.GetSettings();
            if (settings.AniDb.MyList_UseGenericFileIndex)
                await genericsCache.EnsureLoadedAsync(cancellationToken);
            var request = requestFactory.Create<RequestMylist>(
                r =>
                {
                    r.Username = settings.AniDb.Username!;
                    r.Password = settings.AniDb.Password!;
                }
            );
            var response = request.Send();

            if (response.Response is null)
                throw new Exception($"AniDB did not return a successful code: {response.Code}");

            // the cache carries forward the ED2K, size and generic-ness a fetch
            // cannot rediscover, so what it hands back is what to work from
            var entries = mylistCache.ReplaceAll(EnrichEntries(response.Response));
            await CreateEntriesBackup(entries, settings);
            return entries;
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    /// <summary>
    /// Refreshes the cache over HTTP when the fetch mode allows it and the
    /// cache is stale, or the time check is ignored. Failures are logged
    /// and swallowed, so the caller can continue with the cache and UDP.
    /// </summary>
    private async Task RefreshCacheIfAllowedAsync(MylistFetchMode fetchMode, CancellationToken cancellationToken)
    {
        if (!fetchMode.HasFlag(MylistFetchMode.Http)) return;
        if (!fetchMode.HasFlag(MylistFetchMode.IgnoreTimeCheck) && IsCacheFresh()) return;

        try
        {
            await FetchMylistAsync(cancellationToken);
        }
        catch (AnidbHttpBannedException ex)
        {
            _logger.LogWarning("Got an AniDB HTTP ban while refreshing the MyList cache. Expires: {ExpiresAt}", ex.ExpiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh the MyList cache over HTTP");
        }
    }

    private bool IsCacheFresh()
        => mylistCache.LastFetchedAt is { } lastFetched && DateTime.UtcNow - lastFetched < CacheLifetime;

    private async Task CreateEntriesBackup(IReadOnlyList<MylistEntry> entries, IServerSettings settings)
    {
        var backupDirectory = MylistBackups.DirectoryFor(applicationPaths);
        backupDirectory.Create();
        var backupPath = Path.Join(backupDirectory.FullName, MylistBackups.NameFor(DateTimeOffset.UtcNow));
        try
        {
            var serialized = JsonConvert.SerializeObject(entries, Formatting.Indented);
            await using (var fileStream = new FileStream(backupPath, FileMode.Create, FileAccess.Write, FileShare.None))
            await using (var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal))
            await using (var writer = new StreamWriter(gzipStream))
                await writer.WriteAsync(serialized);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write the MyList backup to {Path}", backupPath);
            return;
        }

        if (settings.AniDb.MyList_RetainedBackupCount < 0)
            return;

        var backupFiles = backupDirectory.GetFiles(MylistBackups.RotationPattern).OrderByDescending(f => f.Name).ToList();
        foreach (var file in backupFiles.Skip(settings.AniDb.MyList_RetainedBackupCount))
        {
            try
            {
                file.Delete();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to rotate out the MyList backup {Path}", file.FullName);
            }
        }
    }

    private IReadOnlyList<MylistEntry> EnrichEntries(IEnumerable<MylistEntry> entries)
    {
        // the export says nothing about which entries are generic, so resolve it
        // from the index when we have one and leave it unknown when we do not
        var useGenericsIndex = settingsProvider.GetSettings().AniDb.MyList_UseGenericFileIndex && genericsCache.IsAvailable;
        var enriched = new List<MylistEntry>();
        foreach (var entry in entries)
        {
            var resolved = useGenericsIndex ? entry with { IsGeneric = genericsCache.Contains(entry.FileID) } : entry;
            if (resolved is { ED2K: not null, Size: > 0 })
            {
                enriched.Add(resolved);
                continue;
            }

            var releaseInfo = storedReleaseInfos.GetByReleaseURI($"{AnidbReleaseProvider.ReleasePrefix}{resolved.FileID}");
            enriched.Add(releaseInfo is null ? resolved : resolved with { ED2K = releaseInfo.ED2K, Size = releaseInfo.FileSize });
        }

        return enriched;
    }

    #endregion

    #endregion

    #region Add

    public async Task<MylistEntry?> AddEntryAsync(int fileID, MylistAddData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MylistFetchMode.None)
            return mylistCache.GetByFileID(fileID);

        _logger.LogInformation("Adding a MyList entry. (FileID={FileID})", fileID);

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        data = ResolveAddData(data);

        // short-circuit when the cached entry is already in the desired state
        if (fetchMode.HasFlag(MylistFetchMode.Cache) && mylistCache.GetByFileID(fileID) is { } cachedEntry &&
            MylistCache.IsInDesiredState(cachedEntry, data))
        {
            _logger.LogInformation("Skipping the MyList entry add; it is already in the desired state. (FileID={FileID}, MylistID={MylistID})", fileID, cachedEntry.MylistID);
            return cachedEntry;
        }

        MylistEntry? mylistEntry;
        var request = requestFactory.Create<RequestAddMylist>(
            r =>
            {
                ApplyAddData(r, data);
                r.FileID = (ulong)fileID;
            }
        );
        var response = request.Send();
        MylistEntry? patched = null;

        if (response.Code == UDPReturnCode.FILE_ALREADY_IN_MYLIST)
        {
            var updateRequest = requestFactory.Create<RequestUpdateMylist>(
                r =>
                {
                    ApplyUpdateData(r, data);
                    r.FileID = (ulong)fileID;
                }
            );
            // the add returned the entry as it was *before* this edit, so fold the
            // edit in rather than caching the stale copy
            if (updateRequest.Send().Code is UDPReturnCode.MYLIST_ENTRY_EDITED && response.Response is { } stale)
                patched = PatchEntry(stale, data);
        }

        // keep the local cache in sync with upstream
        mylistEntry = patched ?? response.Response;
        if (mylistEntry is not null) mylistCache.Upsert(mylistEntry);

        return mylistEntry;
    }

    public Task ScheduleAddEntry(int fileID, MylistAddData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<AddAniDBMylistEntryJob>(a =>
        {
            a.FileID = fileID;
            a.Data = data;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<MylistEntry?> AddEntryAsync(string ed2k, long fileSize, MylistAddData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MylistFetchMode.None)
            return mylistCache.GetByEd2k(ed2k, fileSize);

        _logger.LogInformation("Adding a MyList entry. (ED2K={Hash}, Size={Size})", ed2k, fileSize);

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        data = ResolveAddData(data);

        // short-circuit when the cached entry is already in the desired state
        if (fetchMode.HasFlag(MylistFetchMode.Cache) && mylistCache.GetByEd2k(ed2k, fileSize) is { } cachedEntry &&
            MylistCache.IsInDesiredState(cachedEntry, data))
        {
            _logger.LogInformation("Skipping the MyList entry add; it is already in the desired state. (ED2K={Hash}, Size={Size}, MylistID={MylistID})", ed2k, fileSize, cachedEntry.MylistID);
            return cachedEntry;
        }

        MylistEntry? mylistEntry;
        var request = requestFactory.Create<RequestAddMylist>(
            r =>
            {
                ApplyAddData(r, data);
                r.ED2K = ed2k;
                r.Size = fileSize;
            }
        );
        var response = request.Send();
        MylistEntry? patched = null;

        if (response.Code == UDPReturnCode.FILE_ALREADY_IN_MYLIST)
        {
            var updateRequest = requestFactory.Create<RequestUpdateMylist>(
                r =>
                {
                    ApplyUpdateData(r, data);
                    r.ED2K = ed2k;
                    r.Size = fileSize;
                }
            );
            // the add returned the entry as it was *before* this edit, so fold the
            // edit in rather than caching the stale copy
            if (updateRequest.Send().Code is UDPReturnCode.MYLIST_ENTRY_EDITED && response.Response is { } stale)
                patched = PatchEntry(stale, data);
        }

        // keep the local cache in sync with upstream
        mylistEntry = patched ?? response.Response;
        if (mylistEntry is not null) mylistCache.Upsert(mylistEntry with { ED2K = ed2k, Size = fileSize, IsGeneric = false });

        return mylistEntry;
    }

    public Task ScheduleAddEntry(string ed2k, long fileSize, MylistAddData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<AddAniDBMylistEntryJob>(a =>
        {
            a.ED2K = ed2k;
            a.FileSize = fileSize;
            a.Data = data;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<MylistEntry?> AddEntryAsync(int animeID, EpisodeType episodeType, int episodeNumber, MylistAddData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MylistFetchMode.None)
        {
            var noneEpisode = anidbEpisodes.GetByAnimeIDAndEpisodeTypeNumber(animeID, episodeType, episodeNumber).FirstOrDefault();
            return noneEpisode is null ? null : mylistCache.GetByEpisodeID(noneEpisode.EpisodeID);
        }

        _logger.LogInformation("Adding a MyList entry. (AnimeID={AnimeID}, EpisodeType={EpisodeType}, EpisodeNumber={EpisodeNumber})", animeID, episodeType, episodeNumber);

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        data = ResolveAddData(data);

        // short-circuit when the cached entry is already in the desired state
        var anidbEpisode = anidbEpisodes.GetByAnimeIDAndEpisodeTypeNumber(animeID, episodeType, episodeNumber).FirstOrDefault();
        if (fetchMode.HasFlag(MylistFetchMode.Cache) && anidbEpisode is not null && mylistCache.GetByEpisodeID(anidbEpisode.EpisodeID) is { } cachedEntry &&
            MylistCache.IsInDesiredState(cachedEntry, data))
        {
            _logger.LogInformation("Skipping the MyList entry add; it is already in the desired state. (AnimeID={AnimeID}, EpisodeType={EpisodeType}, EpisodeNumber={EpisodeNumber}, MylistID={MylistID})", animeID, episodeType, episodeNumber, cachedEntry.MylistID);
            return cachedEntry;
        }

        var request = requestFactory.Create<RequestAddMylist>(
            r =>
            {
                ApplyAddData(r, data);
                r.AnimeID = animeID;
                r.EpisodeNumber = episodeNumber;
                r.EpisodeType = episodeType;
            }
        );
        var response = request.Send();
        MylistEntry? patched = null;

        if (response.Code == UDPReturnCode.FILE_ALREADY_IN_MYLIST)
        {
            var updateRequest = requestFactory.Create<RequestUpdateMylist>(
                r =>
                {
                    ApplyUpdateData(r, data);
                    r.AnimeID = animeID;
                    r.EpisodeNumber = episodeNumber;
                    r.EpisodeType = episodeType;
                }
            );
            // the add returned the entry as it was *before* this edit, so fold the
            // edit in rather than caching the stale copy
            if (updateRequest.Send().Code is UDPReturnCode.MYLIST_ENTRY_EDITED && response.Response is { } stale)
                patched = PatchEntry(stale, data);
        }

        // keep the local cache in sync with upstream
        var mylistEntry = patched ?? response.Response;
        if (mylistEntry is not null && anidbEpisode is not null)
            mylistEntry = mylistEntry with { AnimeID = animeID, EpisodeID = anidbEpisode.EpisodeID, IsGeneric = true };
        if (mylistEntry is not null) mylistCache.Upsert(mylistEntry);

        return mylistEntry;
    }

    public Task ScheduleAddEntry(int animeID, EpisodeType episodeType, int episodeNumber, MylistAddData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<AddAniDBMylistEntryJob>(a =>
        {
            a.AnimeID = animeID;
            a.EpisodeType = episodeType;
            a.EpisodeNumber = episodeNumber;
            a.Data = data;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<MylistEntry?> AddVideoAsync(IVideo video, MylistAddData? data = null, MylistReadStates readStates = MylistReadStates.Auto, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        if (video is not VideoLocal videoLocal)
            throw new ArgumentException("Video must be a VideoLocal object", nameof(video));

        readStates = ResolveReadStates(readStates);
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MylistFetchMode.None)
            return mylistCache.GetByEd2k(videoLocal.Hash, videoLocal.FileSize);

        _logger.LogInformation(
            "Adding the MyList entry for a video. (File={FileName}, ED2K={Hash}, ReadStates={ReadStates})",
            videoLocal.FirstValidPlace?.FileName, videoLocal.Hash, readStates
        );

        var isManualLink = videoLocal.ReleaseInfo is not { } releaseInfo || !(releaseInfo.ReleaseURI?.StartsWith(AnidbReleaseProvider.ReleasePrefix) ?? false);

        // the local watched state is what the entry falls back to when the
        // caller did not ask for a specific one
        var user = users.GetAniDBUser();
        var originalWatchedDate = user is null
            ? null
            : videoLocalUsers.GetByUserAndVideoLocalID(user.JMMUserID, videoLocal.VideoLocalID)?.WatchedDate?.ToUniversalTime();

        MylistEntry? mylistEntry;
        MylistAddData resolvedData;
        if (isManualLink)
        {
            // a manual link has no file of its own on AniDB, so it is added as a
            // generic entry for each linked episode instead
            resolvedData = ResolveAddData(data);
            mylistEntry = null;
            foreach (var episode in videoLocal.AnimeEpisodes)
            {
                if (episode.AniDB_Episode is not { } anidbEpisode)
                    continue;

                // the generic entry stands for the episode rather than for this
                // particular file, so its watched state has to come from — and go
                // back to — the episode's own user data. Several files can map to
                // one episode, and routing each file's watched date through the
                // shared entry would leave the two permanently fighting over it
                var episodeWatchedDate = user is null ? null : episode.GetUserRecord(user.JMMUserID)?.WatchedDate?.ToUniversalTime();
                var episodeData = ResolveAddData(data, episodeWatchedDate);
                var episodeEntry = await AddEntryAsync(anidbEpisode.AnimeID, anidbEpisode.EpisodeType, anidbEpisode.EpisodeNumber, episodeData, fetchMode, cancellationToken)
                    .ConfigureAwait(false);
                if (episodeEntry is null)
                    continue;

                mylistEntry = episodeEntry;
                if (user is not null)
                    await ImportWatchedState(episodeEntry, readStates, episodeWatchedDate, updateDate => userDataService.ImportEpisodeUserData(episode, user, new()
                    {
                        LastPlayedAt = updateDate,
                        LastUpdatedAt = episodeEntry.UpdatedAt.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                    }, "AniDB")).ConfigureAwait(false);
            }
        }
        else
        {
            resolvedData = ResolveAddData(data, originalWatchedDate);
            mylistEntry = await AddEntryAsync(videoLocal.Hash, videoLocal.FileSize, resolvedData, fetchMode, cancellationToken).ConfigureAwait(false);
            if (mylistEntry is not null && user is not null)
                await ImportWatchedState(mylistEntry, readStates, originalWatchedDate, updateDate => userDataService.ImportVideoUserData(videoLocal, user, new()
                {
                    ProgressPosition = TimeSpan.Zero,
                    LastPlayedAt = updateDate,
                    LastUpdatedAt = mylistEntry.UpdatedAt.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                }, "AniDB")).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Added the MyList entry for a video. (File={FileName}, ManualLink={IsManualLink}, WatchedLocally={WatchedLocally}, WatchedOnAniDB={WatchedOnAniDB}, DesiredState={DesiredState}, AniDbState={AniDbState}, ReadStates={ReadStates})",
            videoLocal.FirstValidPlace?.FileName, isManualLink, originalWatchedDate != null,
            mylistEntry?.IsViewed, resolvedData.State, mylistEntry?.State, readStates
        );

        var series = videoLocal.EpisodeCrossReferences.Select(a => a.AnimeID).Distinct().Except([0]).ToArray();
        if (series.Length > 0)
            await Task.WhenAll(series.Select(id => scheduler.Enqueue<RefreshAnimeStatsJob>(a => a.AnimeID = id)));

        return mylistEntry;
    }

    public Task ScheduleAddVideo(IVideo video, MylistAddData? data = null, MylistReadStates readStates = MylistReadStates.Auto, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<AddAniDBMylistEntryJob>(a =>
        {
            a.VideoID = video.ID;
            a.Data = data;
            a.ReadStates = readStates;
            a.FetchMode = fetchMode;
        }, prioritize);

    public Task ScheduleAddAllManualLinks()
    {
        var files = videoLocals.GetManuallyLinkedVideos();
        return Task.WhenAll(files
            .SelectMany(video => video.AnimeEpisodes)
            .DistinctBy(episode => episode.AniDB_EpisodeID)
            .Select(episode =>
            {
                var anidbEpisode = episode.AniDB_Episode!;
                return ScheduleAddEntry(anidbEpisode.AnimeID, anidbEpisode.EpisodeType, anidbEpisode.EpisodeNumber);
            })
        );
    }

    #region Add | Private

    /// <summary>
    /// Imports the watched state AniDB reported back onto whatever the entry
    /// stands for — the video for a real file, the episode for a generic entry.
    /// Only the direction the read states allow is imported, and AniDB is never
    /// updated from here; the storage state carries that.
    /// </summary>
    private static async Task ImportWatchedState(MylistEntry entry, MylistReadStates readStates, DateTime? localWatchedDate, Func<DateTime?, Task> import)
    {
        var watched = entry.ViewedAt is { } viewedAt && !DateTime.UnixEpoch.Equals(viewedAt);
        var watchedLocally = localWatchedDate is not null;
        if (readStates.HasFlag(MylistReadStates.Watched) && watched && !watchedLocally)
            await import(entry.ViewedAt ?? DateTime.Now).ConfigureAwait(false);
        else if (readStates.HasFlag(MylistReadStates.Unwatched) && !watched && watchedLocally)
            await import(null).ConfigureAwait(false);
    }

    /// <summary>
    /// The file state a request should send, or <c>null</c> to leave it alone.
    /// AniDB rejects the whole command with a 505 for a state its UDP validator
    /// does not know, so an unwritable one is dropped instead of taking the
    /// rest of the request down with it.
    /// </summary>
    private MylistFileState? ResolveFileState(MylistFileState? fileState)
    {
        if (fileState is not { } state || state.IsWritable)
            return fileState;

        _logger.LogWarning("Not sending the {FileState} file state; the AniDB UDP API rejects it. The rest of the request is unaffected", state);
        return null;
    }

    /// <summary>
    /// The watched state a request should send, or <c>null</c> to leave it
    /// alone. A watched date on its own means watched, matching what the
    /// request objects' own <c>ViewedAt</c> setters infer.
    /// </summary>
    private static bool? ResolveIsViewed(bool? isViewed, DateTime? viewedAt)
        => isViewed ?? (viewedAt is not null ? true : null);

    /// <summary>
    /// Fills in the defaults an add relies on: the configured storage state and
    /// the local watched state when the caller knows one. Resolving the
    /// fallback into the data keeps it out of the request builders, so every
    /// add path sends exactly the fields the data carries. The caller's data is
    /// never mutated, and resolving already-resolved data is a no-op. The file
    /// state is deliberately left alone; see <see cref="MylistFileState"/>.
    /// </summary>
    private MylistAddData ResolveAddData(MylistAddData? data, DateTime? fallbackWatchedDate = null)
        => new()
        {
            State = data?.State ?? settingsProvider.GetSettings().AniDb.MyList_StorageState,
            FileState = data?.FileState,
            IsViewed = data?.IsViewed ?? (fallbackWatchedDate is not null ? true : null),
            ViewedAt = AniDBExtensions.TruncateToAniDBPrecision(data?.ViewedAt ?? fallbackWatchedDate),
            Storage = data?.Storage,
            Source = data?.Source,
            Other = data?.Other,
        };

    private void ApplyAddData(RequestAddMylist r, MylistAddData data)
    {
        r.State = data.State;
        r.FileState = ResolveFileState(data.FileState);
        // leave the viewed state alone unless the data carries one; the setters
        // coerce a null viewed date into an explicit `viewed=0`, which would send
        // a field the desired-state check never compared
        if (ResolveIsViewed(data.IsViewed, data.ViewedAt) is { } isViewed)
        {
            r.ViewedAt = data.ViewedAt;
            r.IsViewed = isViewed;
        }

        r.Storage = data.Storage;
        r.Source = data.Source;
        r.Other = data.Other;
    }

    #endregion

    #endregion

    #region Update

    public async Task<MylistEntry?> UpdateEntryAsync(ulong listID, MylistUpdateData data, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MylistFetchMode.None)
            return mylistCache.GetByLid(listID);

        _logger.LogInformation("Updating a MyList entry. (MylistID={MylistID})", listID);

        // an update data with no fields set is a no-op
        if (data.IsEmpty)
        {
            _logger.LogInformation("Skipping the MyList entry update; no fields were set. (MylistID={MylistID})", listID);
            return mylistCache.GetByLid(listID);
        }

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        // short-circuit when the cached entry is already in the desired state
        var cachedEntry = mylistCache.GetByLid(listID);
        if (fetchMode.HasFlag(MylistFetchMode.Cache) && cachedEntry is not null &&
            MylistCache.IsInDesiredState(cachedEntry, data))
        {
            _logger.LogInformation("Skipping the MyList entry update; it is already in the desired state. (MylistID={MylistID})", listID);
            return cachedEntry;
        }

        _logger.LogInformation("Sending the MyList entry update. (MylistID={MylistID})", listID);
        var request = requestFactory.Create<RequestUpdateMylist>(
            r =>
            {
                r.MylistID = listID;
                ApplyUpdateData(r, data);
            }
        );

        var code = request.Send().Code;
        return PersistUpdate(code, cachedEntry, data, () => requestFactory.Create<RequestGetMylist>(r => r.MylistID = listID));
    }

    public Task ScheduleUpdateEntry(ulong listID, MylistUpdateData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<UpdateAniDBMylistEntryJob>(a =>
        {
            a.MylistID = listID;
            a.Data = data;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<MylistEntry?> UpdateEntryAsync(int fileID, MylistUpdateData data, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MylistFetchMode.None)
            return mylistCache.GetByFileID(fileID);

        _logger.LogInformation("Updating a MyList entry. (FileID={FileID})", fileID);

        // an update data with no fields set is a no-op
        if (data.IsEmpty)
        {
            _logger.LogInformation("Skipping the MyList entry update; no fields were set. (FileID={FileID})", fileID);
            return mylistCache.GetByFileID(fileID);
        }

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        // short-circuit when the cached entry is already in the desired state
        var cachedEntry = mylistCache.GetByFileID(fileID);
        if (fetchMode.HasFlag(MylistFetchMode.Cache) && cachedEntry is not null &&
            MylistCache.IsInDesiredState(cachedEntry, data))
        {
            _logger.LogInformation("Skipping the MyList entry update; it is already in the desired state. (MylistID={MylistID})", cachedEntry.MylistID);
            return cachedEntry;
        }

        _logger.LogInformation("Sending the MyList entry update. (FileID={FileID})", fileID);
        var request = requestFactory.Create<RequestUpdateMylist>(
            r =>
            {
                r.FileID = (ulong)fileID;
                ApplyUpdateData(r, data);
            }
        );

        var code = request.Send().Code;
        return PersistUpdate(code, cachedEntry, data, () => requestFactory.Create<RequestGetMylist>(r => r.FileID = (ulong)fileID));
    }

    public Task ScheduleUpdateEntry(int fileID, MylistUpdateData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<UpdateAniDBMylistEntryJob>(a =>
        {
            a.FileID = fileID;
            a.Data = data;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<MylistEntry?> UpdateEntryAsync(string ed2k, long fileSize, MylistUpdateData data, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MylistFetchMode.None)
            return mylistCache.GetByEd2k(ed2k, fileSize);

        _logger.LogInformation("Updating a MyList entry. (ED2K={Hash}, Size={Size})", ed2k, fileSize);

        // an update data with no fields set is a no-op
        if (data.IsEmpty)
        {
            _logger.LogInformation("Skipping the MyList entry update; no fields were set. (ED2K={Hash}, Size={Size})", ed2k, fileSize);
            return mylistCache.GetByEd2k(ed2k, fileSize);
        }

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        // short-circuit when the cached entry is already in the desired state
        var cachedEntry = mylistCache.GetByEd2k(ed2k, fileSize);
        if (fetchMode.HasFlag(MylistFetchMode.Cache) && cachedEntry is not null &&
            MylistCache.IsInDesiredState(cachedEntry, data))
        {
            _logger.LogInformation("Skipping the MyList entry update; it is already in the desired state. (MylistID={MylistID})", cachedEntry.MylistID);
            return cachedEntry;
        }

        _logger.LogInformation("Sending the MyList entry update. (ED2K={Hash}, Size={Size})", ed2k, fileSize);
        var request = requestFactory.Create<RequestUpdateMylist>(
            r =>
            {
                r.ED2K = ed2k;
                r.Size = fileSize;
                ApplyUpdateData(r, data);
            }
        );

        var code = request.Send().Code;
        return PersistUpdate(code, cachedEntry, data, () => requestFactory.Create<RequestGetMylist>(r =>
        {
            r.ED2K = ed2k;
            r.Size = fileSize;
        }), entry => entry with { ED2K = ed2k, Size = fileSize, IsGeneric = false });
    }

    public Task ScheduleUpdateEntry(string ed2k, long fileSize, MylistUpdateData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<UpdateAniDBMylistEntryJob>(a =>
        {
            a.ED2K = ed2k;
            a.FileSize = fileSize;
            a.Data = data;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<MylistEntry?> UpdateEntryAsync(int animeID, EpisodeType episodeType, int episodeNumber, MylistUpdateData data, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MylistFetchMode.None)
        {
            var noneEpisode = anidbEpisodes.GetByAnimeIDAndEpisodeTypeNumber(animeID, episodeType, episodeNumber).FirstOrDefault();
            return noneEpisode is null ? null : mylistCache.GetByEpisodeID(noneEpisode.EpisodeID);
        }

        _logger.LogInformation("Updating a MyList entry. (AnimeID={AnimeID}, EpisodeType={EpisodeType}, EpisodeNumber={EpisodeNumber})", animeID, episodeType, episodeNumber);

        var anidbEpisode = anidbEpisodes.GetByAnimeIDAndEpisodeTypeNumber(animeID, episodeType, episodeNumber).FirstOrDefault();

        // an update data with no fields set is a no-op
        if (data.IsEmpty)
        {
            _logger.LogInformation("Skipping the MyList entry update; no fields were set. (AnimeID={AnimeID}, EpisodeType={EpisodeType}, EpisodeNumber={EpisodeNumber})", animeID, episodeType, episodeNumber);
            return anidbEpisode is null ? null : mylistCache.GetByEpisodeID(anidbEpisode.EpisodeID);
        }

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        // short-circuit when the cached entry is already in the desired state
        var cachedEntry = anidbEpisode is null ? null : mylistCache.GetByEpisodeID(anidbEpisode.EpisodeID);
        if (fetchMode.HasFlag(MylistFetchMode.Cache) && cachedEntry is not null &&
            MylistCache.IsInDesiredState(cachedEntry, data))
        {
            _logger.LogInformation("Skipping the MyList entry update; it is already in the desired state. (MylistID={MylistID})", cachedEntry.MylistID);
            return cachedEntry;
        }

        _logger.LogInformation("Sending the MyList entry update. (AnimeID={AnimeID}, EpisodeType={EpisodeType}, EpisodeNumber={EpisodeNumber})", animeID, episodeType, episodeNumber);
        var request = requestFactory.Create<RequestUpdateMylist>(
            r =>
            {
                r.AnimeID = animeID;
                r.EpisodeNumber = episodeNumber;
                r.EpisodeType = episodeType;
                ApplyUpdateData(r, data);
            }
        );

        var code = request.Send().Code;
        return PersistUpdate(code, cachedEntry, data, () => requestFactory.Create<RequestGetMylist>(r =>
        {
            r.AnimeID = animeID;
            r.EpisodeType = episodeType;
            r.EpisodeNumber = episodeNumber;
        }), entry => anidbEpisode is null ? entry : entry with { AnimeID = animeID, EpisodeID = anidbEpisode.EpisodeID, IsGeneric = true });
    }

    public Task ScheduleUpdateEntry(int animeID, EpisodeType episodeType, int episodeNumber, MylistUpdateData? data = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<UpdateAniDBMylistEntryJob>(a =>
        {
            a.AnimeID = animeID;
            a.EpisodeType = episodeType;
            a.EpisodeNumber = episodeNumber;
            a.Data = data;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<MylistEntry?> UpdateVideoAsync(IVideo video, MylistUpdateData data, bool updateSeriesStats = false, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating the MyList entries for a video. (VideoID={VideoID})", video.ID);

        var vid = videoLocals.GetByID(video.ID);
        if (vid == null)
            return null;

        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MylistFetchMode.None)
            return mylistCache.GetByEd2k(vid.Hash, vid.FileSize);

        // an update data with no fields set is a no-op
        if (data.IsEmpty)
        {
            _logger.LogInformation("Skipping the video MyList update; no fields were set. (VideoID={VideoID})", video.ID);
            return mylistCache.GetByEd2k(vid.Hash, vid.FileSize);
        }

        MylistEntry? entry;
        if (vid.ReleaseInfo is { } releaseInfo && (releaseInfo.ReleaseURI?.StartsWith(AnidbReleaseProvider.ReleasePrefix) ?? false))
        {
            entry = await UpdateEntryAsync(vid.Hash, vid.FileSize, data, fetchMode, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // a manual link is represented by a generic entry per linked episode
            entry = null;
            var user = users.GetAniDBUser();
            foreach (var episode in vid.AnimeEpisodes)
            {
                if (episode.AniDB_Episode is not { } anidbEpisode)
                    continue;

                entry = await UpdateEntryAsync(anidbEpisode.AnimeID, anidbEpisode.EpisodeType, anidbEpisode.EpisodeNumber, ForEpisode(data, episode, user), fetchMode, cancellationToken)
                    .ConfigureAwait(false) ?? entry;
            }
        }

        if (!updateSeriesStats)
            return entry;

        // update watched stats
        var eps = animeEpisodes.GetByHash(vid.Hash);
        if (eps.Count > 0) await Task.WhenAll(eps.DistinctBy(a => a.AnimeSeriesID).Select(a => seriesService.QueueUpdateStats(a.AnimeSeries!)));
        return entry;
    }

    public Task ScheduleUpdateVideo(IVideo video, MylistUpdateData? data = null, bool updateSeriesStats = false, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<UpdateAniDBMylistEntryJob>(a =>
        {
            a.VideoID = video.ID;
            a.Data = data;
            a.UpdateSeriesStats = updateSeriesStats;
            a.FetchMode = fetchMode;
        }, prioritize);

    #region Update | Private

    /// <summary>
    /// Retargets an update aimed at a video onto one of its episodes. The
    /// generic entry stands for the episode rather than for the file, so its
    /// watched state has to come from the episode's own user data; pushing the
    /// file's watched date into the shared entry would leave several files
    /// mapped to the same episode fighting over it.
    /// </summary>
    private static MylistUpdateData ForEpisode(MylistUpdateData data, AnimeEpisode episode, JMMUser? user)
    {
        if (!data.IsViewed.HasValue && !data.ViewedAt.HasValue)
            return data;

        var watchedDate = user is null ? null : episode.GetUserRecord(user.JMMUserID)?.WatchedDate?.ToUniversalTime();
        return new MylistUpdateData
        {
            State = data.State,
            FileState = data.FileState,
            IsViewed = watchedDate is not null,
            ViewedAt = watchedDate,
            Storage = data.Storage,
            Source = data.Source,
            Other = data.Other,
        };
    }

    /// <summary>
    /// Reconciles the local cache with an edit AniDB just accepted. The edit
    /// only touches the fields it sent, so the cached entry is patched in
    /// place rather than re-fetched; the fetch is only needed when there was
    /// nothing cached to patch.
    /// </summary>
    private MylistEntry? PersistUpdate(
        UDPReturnCode code,
        MylistEntry? cached,
        MylistUpdateData data,
        Func<RequestGetMylist> createGetRequest,
        Func<MylistEntry, MylistEntry>? enrichEntry = null
    )
    {
        if (code is not UDPReturnCode.MYLIST_ENTRY_EDITED)
            return cached;

        if (cached is not null)
        {
            var patched = PatchEntry(cached, data);
            mylistCache.Upsert(patched);
            return patched;
        }

        // the edit already succeeded; a failed read-back only costs the return value
        try
        {
            var entry = createGetRequest().Send().Response;
            if (entry is null) return null;
            if (enrichEntry is not null) entry = enrichEntry(entry);
            mylistCache.Upsert(entry);
            return entry;
        }
        catch (AniDBBannedException)
        {
            _logger.LogWarning("Got an AniDB UDP ban while reading back an edited MyList entry");
            return null;
        }
        catch (UnexpectedUDPResponseException ex)
        {
            _logger.LogWarning("AniDB did not return the edited MyList entry: {Code}", ex.ReturnCode);
            return null;
        }
    }

    /// <summary>
    /// Applies the update data to a cached entry, mirroring what
    /// <see cref="ApplyUpdateData(RequestUpdateMylist, MylistUpdateData)"/>
    /// sends. Fields the update leaves unset keep their previous value, which
    /// is what <c>edit=1</c> does upstream.
    /// </summary>
    private MylistEntry PatchEntry(MylistEntry entry, MylistUpdateData data)
    {
        var patched = entry with
        {
            State = data.State ?? entry.State,
            // an unwritable file state is dropped from the request, so writing it
            // here would cache a value AniDB never accepted
            FileState = ResolveFileState(data.FileState) ?? entry.FileState,
            Storage = data.Storage ?? entry.Storage,
            Source = data.Source ?? entry.Source,
            Other = data.Other ?? entry.Other,
            UpdatedAt = DateOnly.FromDateTime(DateTime.Today),
        };

        if (ResolveIsViewed(data.IsViewed, data.ViewedAt) is not { } isViewed)
            return patched;

        return patched with
        {
            IsViewed = isViewed,
            ViewedAt = isViewed ? AniDBExtensions.TruncateToAniDBPrecision(data.ViewedAt ?? DateTime.UtcNow) : null,
        };
    }

    private void ApplyUpdateData(RequestUpdateMylist r, MylistUpdateData data)
    {
        r.State = data.State;
        r.FileState = ResolveFileState(data.FileState);
        if (ResolveIsViewed(data.IsViewed, data.ViewedAt) is { } isViewed)
        {
            r.ViewedAt = data.ViewedAt;
            r.IsViewed = isViewed;
        }

        r.Storage = data.Storage;
        r.Source = data.Source;
        r.Other = data.Other;
    }

    #endregion

    #endregion

    #region Remove

    public async Task<bool> RemoveEntryAsync(ulong listID, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MylistFetchMode.None)
            return false;

        _logger.LogInformation("Removing a MyList entry. (MylistID={MylistID})", listID);

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        var cached = mylistCache.GetByLid(listID);
        if (cached is null && !fetchMode.HasFlag(MylistFetchMode.Udp))
        {
            _logger.LogInformation("Skipping the MyList entry removal; it is not in the local cache and UDP is not allowed");
            return false;
        }

        var request = requestFactory.Create<RequestRemoveMylist>(r => r.MylistID = listID);
        var code = request.Send().Code;
        if (code == UDPReturnCode.MYLIST_ENTRY_DELETED && cached is not null) mylistCache.Remove(cached);
        return code == UDPReturnCode.MYLIST_ENTRY_DELETED;
    }

    public Task ScheduleRemoveEntry(ulong listID, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<RemoveAniDBMylistEntryJob>(a =>
        {
            a.MylistID = listID;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<bool> RemoveEntryAsync(int fileID, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MylistFetchMode.None)
            return false;

        _logger.LogInformation("Removing a MyList entry. (FileID={FileID})", fileID);

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        var cached = mylistCache.GetByFileID(fileID);
        if (cached is null && !fetchMode.HasFlag(MylistFetchMode.Udp))
        {
            _logger.LogInformation("Skipping the MyList entry removal; it is not in the local cache and UDP is not allowed");
            return false;
        }

        var request = requestFactory.Create<RequestRemoveMylist>(r => r.FileID = (ulong)fileID);
        var code = request.Send().Code;
        if (code == UDPReturnCode.MYLIST_ENTRY_DELETED && cached is not null) mylistCache.Remove(cached);
        return code == UDPReturnCode.MYLIST_ENTRY_DELETED;
    }

    public Task ScheduleRemoveEntry(int fileID, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<RemoveAniDBMylistEntryJob>(a =>
        {
            a.FileID = fileID;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<bool> RemoveEntryAsync(string ed2k, long fileSize, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MylistFetchMode.None)
            return false;

        _logger.LogInformation("Removing a MyList entry. (ED2K={Hash}, Size={Size})", ed2k, fileSize);

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        var cached = mylistCache.GetByEd2k(ed2k, fileSize);
        if (cached is null && !fetchMode.HasFlag(MylistFetchMode.Udp))
        {
            _logger.LogInformation("Skipping the MyList entry removal; it is not in the local cache and UDP is not allowed");
            return false;
        }

        var request = requestFactory.Create<RequestRemoveMylist>(r =>
        {
            r.ED2K = ed2k;
            r.Size = fileSize;
        });
        var code = request.Send().Code;
        if (code == UDPReturnCode.MYLIST_ENTRY_DELETED && cached is not null) mylistCache.Remove(cached);
        return code == UDPReturnCode.MYLIST_ENTRY_DELETED;
    }

    public Task ScheduleRemoveEntry(string ed2k, long fileSize, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<RemoveAniDBMylistEntryJob>(a =>
        {
            a.ED2K = ed2k;
            a.FileSize = fileSize;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<bool> RemoveEntryAsync(int animeID, EpisodeType episodeType, int episodeNumber, MylistFetchMode fetchMode = MylistFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MylistFetchMode.None)
            return false;

        _logger.LogInformation("Removing a MyList entry. (AnimeID={AnimeID}, EpisodeType={EpisodeType}, EpisodeNumber={EpisodeNumber})", animeID, episodeType, episodeNumber);

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        var episode = anidbEpisodes.GetByAnimeIDAndEpisodeTypeNumber(animeID, episodeType, episodeNumber).FirstOrDefault();
        var cached = episode is null ? null : mylistCache.GetByEpisodeID(episode.EpisodeID);
        if (cached is null && !fetchMode.HasFlag(MylistFetchMode.Udp))
        {
            _logger.LogInformation("Skipping the MyList entry removal; it is not in the local cache and UDP is not allowed");
            return false;
        }

        var request = requestFactory.Create<RequestRemoveMylist>(r =>
        {
            r.AnimeID = animeID;
            r.EpisodeType = episodeType;
            r.EpisodeNumber = episodeNumber;
        });
        var code = request.Send().Code;
        if (code == UDPReturnCode.MYLIST_ENTRY_DELETED && cached is not null) mylistCache.Remove(cached);
        return code == UDPReturnCode.MYLIST_ENTRY_DELETED;
    }

    public Task ScheduleRemoveEntry(int animeID, EpisodeType episodeType, int episodeNumber, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<RemoveAniDBMylistEntryJob>(a =>
        {
            a.AnimeID = animeID;
            a.EpisodeType = episodeType;
            a.EpisodeNumber = episodeNumber;
            a.FetchMode = fetchMode;
        }, prioritize);

    public Task ScheduleDisposeEntry(ulong listID, MylistDeleteType? deleteType = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false)
        => DisposeEntry(deleteType, state => ScheduleUpdateEntry(listID, new() { State = state }, fetchMode, prioritize), () => ScheduleRemoveEntry(listID, fetchMode, prioritize));

    public Task ScheduleDisposeEntry(int fileID, MylistDeleteType? deleteType = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false)
        => DisposeEntry(deleteType, state => ScheduleUpdateEntry(fileID, new() { State = state }, fetchMode, prioritize), () => ScheduleRemoveEntry(fileID, fetchMode, prioritize));

    public Task ScheduleDisposeEntry(string ed2k, long fileSize, MylistDeleteType? deleteType = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false)
        => DisposeEntry(deleteType, state => ScheduleUpdateEntry(ed2k, fileSize, new() { State = state }, fetchMode, prioritize), () => ScheduleRemoveEntry(ed2k, fileSize, fetchMode, prioritize));

    public Task ScheduleDisposeEntry(int animeID, EpisodeType episodeType, int episodeNumber, MylistDeleteType? deleteType = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false)
        => DisposeEntry(
            deleteType,
            state => ScheduleUpdateEntry(animeID, episodeType, episodeNumber, new() { State = state }, fetchMode, prioritize),
            () => ScheduleRemoveEntry(animeID, episodeType, episodeNumber, fetchMode, prioritize)
        );

    public async Task ScheduleDisposeVideo(IVideo video, MylistDeleteType? deleteType = null, MylistFetchMode fetchMode = MylistFetchMode.Auto, bool prioritize = false)
    {
        if (videoLocals.GetByID(video.ID) is not { } videoLocal)
            return;

        if (videoLocal.ReleaseInfo is { } releaseInfo && (releaseInfo.ReleaseURI?.StartsWith(AnidbReleaseProvider.ReleasePrefix) ?? false))
        {
            await ScheduleDisposeEntry(videoLocal.Hash, videoLocal.FileSize, deleteType, fetchMode, prioritize);
            return;
        }

        // a manual link has no entry of its own, so what covers it is the generic
        // entry of each linked episode
        foreach (var episode in videoLocal.EpisodeCrossReferences.Select(xref => xref.AniDBEpisode).WhereNotNull())
        {
            // that generic entry covers the episode rather than this one file, so
            // it has to stay while another manually linked release still relies on it
            if (SharesGenericEntry(videoLocal, episode.EpisodeID))
            {
                _logger.LogInformation(
                    "Keeping the generic AniDB MyList entry, another manually linked release still uses it. (AnimeID={AnimeID}, EpisodeType={EpisodeType}, EpisodeNumber={EpisodeNumber})",
                    episode.AnimeID, episode.EpisodeType, episode.EpisodeNumber);
                continue;
            }

            await ScheduleDisposeEntry(episode.AnimeID, episode.EpisodeType, episode.EpisodeNumber, deleteType, fetchMode, prioritize);
        }
    }

    #region Remove | Private

    /// <summary>
    /// Whether another release still maps to the episode and would lose its
    /// MyList coverage if the episode's generic entry went away. Only manual
    /// links count; a release with an AniDB file ID has an entry of its own.
    /// The video being disposed of is excluded, whether or not its release has
    /// already been deleted by the time this runs.
    /// </summary>
    private bool SharesGenericEntry(VideoLocal video, int anidbEpisodeID)
        => storedReleaseInfos.GetByAnidbEpisodeID(anidbEpisodeID)
            .Any(other => (other.ED2K != video.Hash || other.FileSize != video.FileSize)
                && !(other.ReleaseURI?.StartsWith(AnidbReleaseProvider.ReleasePrefix) ?? false));

    /// <summary>
    /// Applies a delete type by picking between the two entry-level operations:
    /// a state update when the entry is only being marked, an outright removal
    /// otherwise. Neither of those needs to know about delete types, so the
    /// choice is made once, here, rather than inside the job.
    /// </summary>
    private Task DisposeEntry(MylistDeleteType? deleteType, Func<MylistState, Task> mark, Func<Task> remove)
    {
        deleteType ??= settingsProvider.GetSettings().AniDb.MyList_DeleteType;
        if (deleteType is MylistDeleteType.DeleteLocalOnly)
            return Task.CompletedTask;

        return GetMarkedState(deleteType.Value) is { } state ? mark(state) : remove();
    }

    /// <summary>
    /// The storage state an entry is marked with for a given delete type, or
    /// <c>null</c> when the entry is to be removed outright rather than marked.
    /// </summary>
    private static MylistState? GetMarkedState(MylistDeleteType deleteType)
        => deleteType switch
        {
            MylistDeleteType.MarkDeleted => MylistState.Deleted,
            MylistDeleteType.MarkExternalStorage => MylistState.Remote,
            MylistDeleteType.MarkUnknown => MylistState.Unknown,
            MylistDeleteType.MarkDisk => MylistState.Disk,
            _ => null,
        };

    #endregion

    #endregion

    #region Sync

    public async Task<MylistSyncResult?> SyncAsync(MylistSyncOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!await _syncLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation("Skipping the MyList sync; one is already running");
            return null;
        }

        try
        {
            return await SyncInternalAsync(options, null, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public Task<MylistSyncResult?> SyncAsync(IEnumerable<IShokoEpisode> episodes, MylistSyncOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(episodes);

        return SyncScopedAsync(BuildSyncScope(episodes), options, cancellationToken);
    }

    public Task ScheduleSync(IEnumerable<IShokoEpisode> episodes, MylistSyncOptions? options = null, bool prioritize = false)
    {
        ArgumentNullException.ThrowIfNull(episodes);
        RejectPlanOnly(options);

        var episodeIDs = episodes.Select(episode => episode.ID).Distinct().ToArray();
        if (episodeIDs.Length is 0)
            return Task.CompletedTask;

        var resolved = ResolveSyncOptions(options);
        return scheduler.Enqueue<SyncAniDBMylistEpisodesJob>(a =>
        {
            a.EpisodeIDs = episodeIDs;
            a.Options = resolved;
        }, prioritize);
    }

    public Task<MylistSyncResult?> SyncAsync(IEnumerable<IVideo> videos, MylistSyncOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(videos);

        return SyncScopedAsync(BuildSyncScope(videos), options, cancellationToken);
    }

    public async Task<MylistSyncResult?> ApplySyncPlanAsync(MylistSyncPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.Actions.Count is 0)
            return new MylistSyncResult { Plan = plan, IsApplied = true };

        ValidatePlan(plan);

        // the same guard a sync takes: applying a plan writes the very state a
        // sync reconciles, so the two cannot be in flight together
        if (!await _syncLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation("Skipping the MyList plan; a sync is already running");
            return null;
        }

        try
        {
            _logger.LogInformation("Applying a MyList plan of {Count} steps, worked out at {CreatedAt}", plan.Actions.Count, plan.CreatedAt);

            var anidbUser = users.GetAniDBUser();
            var applied = 0;
            foreach (var action in plan.Actions)
            {
                try
                {
                    await ApplyAction(action, anidbUser).ConfigureAwait(false);
                    applied++;
                }
                catch (Exception ex)
                {
                    // one unusable step should not take the rest of the plan
                    // down with it; the plan is a list of independent edits
                    _logger.LogError(ex, "A MyList plan step threw and was skipped. ({Description})", action.Description);
                }
            }

            _logger.LogInformation("Applied {Applied} of {Count} MyList plan steps", applied, plan.Actions.Count);
            return new MylistSyncResult { Plan = plan, IsApplied = true };
        }
        finally
        {
            _syncLock.Release();
        }
    }

    /// <summary>
    /// Checks a plan before any of it runs. A plan can be assembled by a plugin
    /// or a client rather than by a sync, so it is checked rather than trusted —
    /// and checked as a whole, so a caller is told everything wrong with it
    /// rather than finding out one step at a time, halfway through.
    /// </summary>
    /// <exception cref="GenericValidationException">
    /// One or more steps cannot be carried out as given.
    /// </exception>
    private static void ValidatePlan(MylistSyncPlan plan)
    {
        var errors = new Dictionary<string, IReadOnlyList<string>>();
        for (var index = 0; index < plan.Actions.Count; index++)
        {
            var action = plan.Actions[index];
            var problems = new List<string>();

            // pairing a list id with the wrong file or episode would edit an
            // entry the caller never named
            if (!action.IsEntryConsistent)
                problems.Add("The MyList entry does not belong to the file or episode the step names.");

            var addressable = action.Kind switch
            {
                MylistSyncActionKind.AlreadyInDesiredState => true,
                MylistSyncActionKind.ImportWatchedState => action.HasVideo || action.HasLocalEpisode,
                MylistSyncActionKind.ExportEntryAddition => action.HasVideo || action.HasEpisode,
                _ => action.HasEntry || action.HasVideo || action.HasEpisode,
            };
            if (!addressable)
                problems.Add(action.Kind is MylistSyncActionKind.ImportWatchedState
                    ? "An import needs a file or an episode that is in the collection."
                    : "The step names nothing that can be acted on.");

            if (problems.Count > 0)
                errors[$"{nameof(plan.Actions)}[{index}]"] = problems;
        }

        if (errors.Count > 0)
            throw new GenericValidationException("The MyList plan cannot be applied as given.", errors);
    }

    /// <summary>
    /// Turns one planned step back into the call that performs it. The identity
    /// fields the step carries are the same ones the entry-level operations take,
    /// so this is a dispatch rather than a re-derivation.
    /// </summary>
    private async Task ApplyAction(MylistSyncAction action, JMMUser? anidbUser)
    {
        // an entry carries every way AniDB can be told about it, and the video
        // and episode carry the rest, so a step needs no identifiers of its own
        var entry = action.Entry;
        var anidbEpisode = action.AnidbEpisode;
        switch (action.Kind)
        {
            case MylistSyncActionKind.ImportWatchedState:
                if (anidbUser is null)
                    return;

                if (action.Video is { } importVideo && videoLocals.GetByID(importVideo.ID) is { } video)
                    await userDataService.ImportVideoUserData(video, anidbUser, new()
                    {
                        ProgressPosition = TimeSpan.Zero,
                        LastPlayedAt = action.WatchedAt,
                    }, "AniDB", false).ConfigureAwait(false);
                else if (action.ShokoEpisode is { } importEpisode && animeEpisodes.GetByID(importEpisode.ID) is { } episode)
                    await userDataService.ImportEpisodeUserData(episode, anidbUser, new()
                    {
                        LastPlayedAt = action.WatchedAt,
                    }, "AniDB", VideoUserDataSaveReason.None, false).ConfigureAwait(false);
                return;

            case MylistSyncActionKind.ExportWatchedState:
                var data = new MylistUpdateData { State = action.State, IsViewed = action.WatchedAt is not null, ViewedAt = action.WatchedAt };
                if (entry is { MylistID: not 0 })
                    await ScheduleUpdateEntry(entry.MylistID, data);
                else if (entry is { FileID: not 0 })
                    await ScheduleUpdateEntry(entry.FileID, data);
                else if (action.Video is { } updateVideo)
                    await ScheduleUpdateEntry(updateVideo.ED2K, updateVideo.Size, data);
                else if (anidbEpisode is not null)
                    await ScheduleUpdateEntry(anidbEpisode.SeriesID, anidbEpisode.Type, anidbEpisode.EpisodeNumber, data);
                return;

            case MylistSyncActionKind.ExportEntryAddition:
                var addData = action.WatchedAt is null ? null : new MylistAddData { IsViewed = true, ViewedAt = action.WatchedAt };
                if (action.Video is { } addVideo)
                    await ScheduleAddEntry(addVideo.ED2K, addVideo.Size, addData);
                else if (anidbEpisode is not null)
                    await ScheduleAddEntry(anidbEpisode.SeriesID, anidbEpisode.Type, anidbEpisode.EpisodeNumber, addData);
                return;

            // planned only so the caller can see it; carrying it out would
            // write the values the entry already holds
            case MylistSyncActionKind.AlreadyInDesiredState:
                return;

            case MylistSyncActionKind.ExportEntryRemoval:
                if (entry is { MylistID: not 0 })
                    await ScheduleDisposeEntry(entry.MylistID, action.DeleteType);
                else if (entry is { FileID: not 0 })
                    await ScheduleDisposeEntry(entry.FileID, action.DeleteType);
                else if (action.Video is { } removeVideo)
                    await ScheduleDisposeEntry(removeVideo.ED2K, removeVideo.Size, action.DeleteType);
                else if (anidbEpisode is not null)
                    await ScheduleDisposeEntry(anidbEpisode.SeriesID, anidbEpisode.Type, anidbEpisode.EpisodeNumber, action.DeleteType);
                return;
        }
    }

    private async Task<MylistSyncResult?> SyncScopedAsync(SyncScope scope, MylistSyncOptions? options, CancellationToken cancellationToken)
    {
        // nothing in scope is not the same as nothing to report: hand back an
        // empty plan rather than a null, which means a sync was already running
        if (scope.IsEmpty)
            return new MylistSyncResult
            {
                Plan = new MylistSyncPlan { Actions = [], CreatedAt = DateTime.UtcNow },
                IsApplied = !(options?.PlanOnly ?? false),
            };

        // the same guard as the full sync; the two overlap on the cache and on
        // the entries they would reconcile, so they cannot run side by side
        if (!await _syncLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation("Skipping the MyList sync; one is already running");
            return null;
        }

        try
        {
            return await SyncInternalAsync(options, scope, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task<MylistSyncResult> SyncInternalAsync(MylistSyncOptions? options, SyncScope? scope, CancellationToken cancellationToken)
    {
        var fetchMode = ResolveFetchMode(options?.FetchMode ?? MylistFetchMode.Auto);
        var ignoreTimeCheck = fetchMode.HasFlag(MylistFetchMode.IgnoreTimeCheck);
        var settings = settingsProvider.GetSettings();

        var resolved = ResolveSyncOptions(options);
        var readWatched = resolved.ReadWatched!.Value;
        var readUnwatched = resolved.ReadUnwatched!.Value;
        var setWatched = resolved.SetWatched!.Value;
        var setUnwatched = resolved.SetUnwatched!.Value;
        var watchedSyncMode = resolved.WatchedSyncMode!.Value;
        var updateStates = resolved.UpdateStates!.Value;
        var storageState = resolved.StorageState!.Value;
        var deleteType = resolved.DeleteType!.Value;
        var targets = resolved.Targets!.Value;
        var watchedEpisodeMode = resolved.WatchedEpisodeMode!.Value;

        // read from the caller's options rather than the resolved ones: a preview
        // is a property of this call, not something the settings can turn on
        var planOnly = options?.PlanOnly ?? false;
        if (planOnly)
            if (scope is null)
                _logger.LogInformation("Planning the MyList sync");
            else
                _logger.LogInformation("Planning the MyList sync for {Count} videos", scope.VideoIDs.Count);
        else
            if (scope is null)
                _logger.LogInformation("Syncing the MyList");
            else
                _logger.LogInformation("Syncing the MyList for {Count} videos", scope.VideoIDs.Count);

        var actions = new List<MylistSyncAction>();

        var entries = ignoreTimeCheck
            ? await FetchMylistAsync(cancellationToken)
            : await GetEntriesAsync(fetchMode, cancellationToken);

        if (settings.AniDb.MyList_UseGenericFileIndex && !genericsCache.IsAvailable)
            _logger.LogWarning("The generic file ID index is unavailable; falling back to the file state to identify generic entries");

        var totalItems = 0;
        var watchedItems = 0;
        var modifiedItems = 0;
        var unclassifiedItems = 0;

        // Add missing files on AniDB
        var onlineFiles = entries
            .ToLookup(a => a.FileID);
        var localFiles = storedReleaseInfos.GetAll()
            .Where(r => !string.IsNullOrEmpty(r.ReleaseURI) && r.ReleaseURI.StartsWith(AnidbReleaseProvider.ReleasePrefix))
            .ToLookup(a => a.ED2K);

        var missingFiles = await AddMissingFiles(localFiles, onlineFiles, scope, planOnly, actions);

        var anidbUser = users.GetAniDBUser();
        var modifiedSeries = new LinkedHashSet<AnimeSeries>();

        // Remove Missing Files and update watched states (single loop)
        var filesToRemove = new List<MylistEntry>();
        var episodesToRemove = new List<MylistEntry>();

        foreach (var myItem in onlineFiles.SelectMany(a => a))
        {
            try
            {
                // Tier 1 (file level): entries for real files are matched to local files by their FileID
                // Tier 2 (episode level): generic entries are matched to episodes
                // Any entry that cannot be matched is treated as missing
                //
                // the entry carries the answer when the index resolved one; otherwise
                // all we have is the _generic_ file state, a convention plenty of
                // generic entries do not follow
                var isGeneric = myItem.IsGeneric ?? myItem.FileState is not (MylistFileState.Normal or MylistFileState.Corrupted);
                var episode = isGeneric && myItem.EpisodeID is not 0 ? animeEpisodes.GetByAniDBEpisodeID(myItem.EpisodeID) : null;
                var video = isGeneric
                    ? null
                    : storedReleaseInfos.GetByReleaseURI($"{AnidbReleaseProvider.ReleasePrefix}{myItem.FileID}") is { ED2K: { } ed2k }
                        ? videoLocals.GetByEd2k(ed2k)
                        : null;

                // a scoped sync only reconciles what the caller handed it, so an
                // entry belonging to anything else is not this run's business
                if (scope is not null && !scope.Covers(episode, video))
                    continue;

                // the two tiers are independent, so a sync can be asked for either
                if (!targets.HasFlag(isGeneric ? MylistSyncTargets.Episodes : MylistSyncTargets.Videos))
                    continue;

                totalItems++;
                if (myItem.ViewedAt.HasValue) watchedItems++;

                if (episode is not null)
                {
                    // a generic entry exists to record a watch for an episode with no
                    // file. With no file, no local watch and nothing watched upstream
                    // either, it records nothing and is left over rather than wanted
                    if (deleteType is not MylistDeleteType.DeleteLocalOnly &&
                        !myItem.IsViewed && episode.VideoLocals.Count is 0 && LocalWatchedDate(anidbUser, episode) is null)
                    {
                        episodesToRemove.Add(myItem);
                        continue;
                    }

                    modifiedItems = await ProcessGenericEntry(anidbUser, episode, myItem, modifiedItems, modifiedSeries, storageState, readWatched, readUnwatched, setWatched, setUnwatched, watchedSyncMode, updateStates, planOnly, actions);
                    continue;
                }

                if (video is not null)
                {
                    // We have it, so process watched states and update storage states if needed
                    modifiedItems = await ProcessStates(anidbUser, video, myItem, modifiedItems, modifiedSeries, storageState, readWatched, readUnwatched, setWatched, setUnwatched, watchedSyncMode, updateStates, planOnly, actions);
                    continue;
                }

                // an unmatched entry means the local file is gone, which cannot be
                // true of a video the caller handed in, so a scoped sync never removes
                if (scope is not null)
                    continue;

                if (deleteType is MylistDeleteType.DeleteLocalOnly)
                    continue;

                // We could not tell whether this entry is generic, so we do not know
                // which tier should have matched it and cannot say it is really
                // missing. Disposing of it on a guess risks removing a generic entry
                // from the user's AniDB MyList over a file state that never meant
                // what we read into it, so leave it alone
                if (myItem.IsGeneric is null)
                {
                    unclassifiedItems++;
                    continue;
                }

                filesToRemove.Add(myItem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A MyList Item threw an error while syncing");
            }
        }

        if (filesToRemove.Count > 0)
        {
            foreach (var entry in filesToRemove)
            {
                actions.Add(new MylistSyncAction
                {
                    Kind = MylistSyncActionKind.ExportEntryRemoval,
                    Description = $"Remove the MyList entry for file {entry.FileID}, which is no longer in the library",
                    Entry = entry,
                    DeleteType = deleteType,
                });
                if (planOnly)
                    continue;

                // entries from the HTTP export always carry a list ID — the cheapest
                // identification mode for either operation
                if (entry.MylistID is not 0)
                    await ScheduleDisposeEntry(entry.MylistID, deleteType);
                else if (entry.FileID is not 0)
                    await ScheduleDisposeEntry(entry.FileID, deleteType);
                // a generic entry we added ourselves has neither yet: AniDB answers a
                // generic add with the number of entries added rather than a list ID,
                // so it stays keyed by episode until the next fetch supplies one
                else if (entry.IsGeneric is true && entry.EpisodeID is not 0 && anidbEpisodes.GetByEpisodeID(entry.EpisodeID) is { } anidbEpisode)
                    await ScheduleDisposeEntry(anidbEpisode.AnimeID, anidbEpisode.EpisodeType, anidbEpisode.EpisodeNumber, deleteType);
                else
                    _logger.LogWarning("Unable to dispose of a MyList entry with no list ID, file ID or resolvable episode. (AnimeID={AnimeID}, EpisodeID={EpisodeID})", entry.AnimeID, entry.EpisodeID);
            }
        }

        if (filesToRemove.Count > 0)
            _logger.LogInformation("MYLIST Missing Files: {Count} added to queue for deletion",
                filesToRemove.Count);

        foreach (var entry in episodesToRemove)
        {
            var anidbEpisode = animeEpisodes.GetByAniDBEpisodeID(entry.EpisodeID)?.AniDB_Episode;
            actions.Add(new MylistSyncAction
            {
                Kind = MylistSyncActionKind.ExportEntryRemoval,
                Description = $"Remove the generic MyList entry for episode {entry.EpisodeID}, which records nothing",
                Entry = entry,
                // the local episode only rides along with the AniDB one it
                // belongs to, so a consumer that has the former always has both
                ShokoEpisode = anidbEpisode is null ? null : animeEpisodes.GetByAniDBEpisodeID(entry.EpisodeID),
                AnidbEpisode = anidbEpisode,
                DeleteType = deleteType,
            });
            if (planOnly)
                continue;

            if (entry.MylistID is not 0)
                await ScheduleDisposeEntry(entry.MylistID, deleteType);
            else if (anidbEpisode is not null)
                await ScheduleDisposeEntry(anidbEpisode.AnimeID, anidbEpisode.EpisodeType, anidbEpisode.EpisodeNumber, deleteType);
        }

        if (episodesToRemove.Count > 0)
            _logger.LogInformation("MYLIST Vestigial Episodes: {Count} added to queue for deletion", episodesToRemove.Count);

        var episodesAdded = targets.HasFlag(MylistSyncTargets.Episodes)
            ? await AddMissingEpisodes(entries, scope, anidbUser, watchedEpisodeMode, planOnly, actions)
            : 0;

        await Task.WhenAll(modifiedSeries.Select(a => seriesService.QueueUpdateStats(a)));

        if (unclassifiedItems > 0)
            _logger.LogWarning(
                "MYLIST Unclassified: left {Count} unmatched entries alone because we could not tell whether they are generic. Enable {Setting} to resolve them",
                unclassifiedItems, nameof(AniDbSettings.MyList_UseGenericFileIndex));

        _logger.LogInformation(
            "Process MyList: {TotalItems} Items, {MissingFiles} Added, {Count} Deleted, {WatchedItems} Watched, {ModifiedItems} Modified, {UnclassifiedItems} Unclassified",
            totalItems, missingFiles, filesToRemove.Count, watchedItems, modifiedItems, unclassifiedItems);

        return new MylistSyncResult
        {
            TotalEntries = totalItems,
            WatchedEntries = watchedItems,
            ModifiedEntries = modifiedItems,
            FilesQueuedForAdd = missingFiles,
            EntriesQueuedForRemoval = filesToRemove.Count,
            UnclassifiedEntries = unclassifiedItems,
            EpisodesQueuedForAdd = episodesAdded,
            EpisodesQueuedForRemoval = episodesToRemove.Count,
            Plan = new MylistSyncPlan { Actions = actions, CreatedAt = DateTime.UtcNow },
            IsApplied = !planOnly,
        };
    }

    public Task ScheduleSync(MylistSyncOptions? options = null, bool prioritize = false)
    {
        RejectPlanOnly(options);

        // resolve up front, so the job carries a complete set by the time it
        // executes. Its options are non-nullable by design, so anything enqueued
        // without them would silently run on the job's defaults instead of the
        // user's settings
        var resolved = ResolveSyncOptions(options);
        return scheduler.Enqueue<SyncAniDBMylistJob>(a => a.Options = resolved, prioritize);
    }

    public Task ScheduleSync(IEnumerable<IVideo> videos, MylistSyncOptions? options = null, bool prioritize = false)
    {
        ArgumentNullException.ThrowIfNull(videos);
        RejectPlanOnly(options);

        var videoIDs = videos.Select(video => video.ID).Distinct().ToArray();
        if (videoIDs.Length is 0)
            return Task.CompletedTask;

        var resolved = ResolveSyncOptions(options);
        return scheduler.Enqueue<SyncAniDBMylistVideosJob>(a =>
        {
            a.VideoIDs = videoIDs;
            a.Options = resolved;
        }, prioritize);
    }

    /// <summary>
    /// A preview exists to hand its plan back to the caller, and a queued job
    /// has nowhere to hand one. Asking to schedule one is a mistake rather than
    /// something to quietly ignore, so it is refused outright — the alternative
    /// is running a real sync that writes exactly what the caller asked not to.
    /// </summary>
    private static void RejectPlanOnly(MylistSyncOptions? options)
    {
        if (options?.PlanOnly is true)
            throw new ArgumentException("A MyList sync preview cannot be scheduled, because its plan would have nowhere to go. Call SyncAsync instead.", nameof(options));
    }

    /// <summary>
    /// Fills every null field of <paramref name="options"/> in from the
    /// configured settings, so the result is fully resolved.
    /// </summary>
    private MylistSyncOptions ResolveSyncOptions(MylistSyncOptions? options)
    {
        var settings = settingsProvider.GetSettings();
        return new MylistSyncOptions
        {
            FetchMode = options?.FetchMode ?? settings.AniDb.MyList_FetchMode,
            ReadWatched = options?.ReadWatched ?? settings.AniDb.MyList_ReadWatched,
            ReadUnwatched = options?.ReadUnwatched ?? settings.AniDb.MyList_ReadUnwatched,
            SetWatched = options?.SetWatched ?? settings.AniDb.MyList_SetWatched,
            SetUnwatched = options?.SetUnwatched ?? settings.AniDb.MyList_SetUnwatched,
            WatchedSyncMode = options?.WatchedSyncMode ?? settings.AniDb.MyList_WatchedSyncMode,
            UpdateStates = options?.UpdateStates ?? settings.AniDb.MyList_UpdateStates,
            StorageState = options?.StorageState ?? settings.AniDb.MyList_StorageState,
            DeleteType = options?.DeleteType ?? settings.AniDb.MyList_DeleteType,
            Targets = options?.Targets ?? settings.AniDb.MyList_SyncTargets,
            WatchedEpisodeMode = options?.WatchedEpisodeMode ?? settings.AniDb.MyList_WatchedEpisodeMode,
        };
    }

    #region Sync | Private

    /// <summary>
    /// Reconciles one entry's watched state and storage state. The rules are in
    /// <see cref="MylistSyncDecisions.DecideWatchedAction"/>; what differs
    /// between a file entry and a generic one is only where the local date comes
    /// from, how an import is written back, and how the entry is identified.
    ///
    /// <c>canImport</c> is false when there is no AniDB user to write an import
    /// for. The decision is then dropped rather than falling through to an
    /// export, which is what the branch it replaced did.
    /// </summary>
    private async Task<int> ReconcileEntry(
        MylistEntry myItem,
        DateTime? localWatchedDate,
        int modifiedItems,
        MylistState localState,
        bool readWatched,
        bool readUnwatched,
        bool setWatched,
        bool setUnwatched,
        MylistWatchedSyncMode watchedSyncMode,
        bool updateStates,
        bool canImport,
        bool planOnly,
        List<MylistSyncAction> actions,
        Func<DateTime?, Task> import,
        Action<UpdateAniDBMylistEntryJob> identify,
        Func<MylistSyncActionKind, DateTime?, MylistState?, MylistSyncAction> describe
    )
    {
        var action = MylistSyncDecisions.DecideWatchedAction(
            localWatchedDate, myItem.ViewedAt, myItem.UpdatedAt,
            readWatched, readUnwatched, setWatched, setUnwatched, watchedSyncMode
        );

        // an export carries the decided date; anything else leaves the entry's
        // own date in place, so a storage-state-only update re-sends it unchanged
        var exportDate = myItem.ViewedAt;
        var shouldUpdate = false;
        switch (action.Kind)
        {
            case MylistWatchedActionKind.Import when canImport:
                modifiedItems++;
                actions.Add(describe(MylistSyncActionKind.ImportWatchedState, action.Date, null));
                if (!planOnly)
                    await import(action.Date).ConfigureAwait(false);
                break;

            case MylistWatchedActionKind.Export:
                shouldUpdate = true;
                exportDate = action.Date;
                break;
        }

        if (updateStates && (int)myItem.State != (int)localState) shouldUpdate = true;

        if (!shouldUpdate)
            return modifiedItems;

        // the rules decided to write, so this should always be a real change. If
        // it is not, the decision and the desired-state check disagree and the
        // entry will be planned again on every sync — worth surfacing, not hiding
        var desired = new MylistUpdateData { State = updateStates ? localState : null, IsViewed = exportDate is not null, ViewedAt = exportDate };
        actions.Add(describe(
            MylistCache.IsInDesiredState(myItem, desired) ? MylistSyncActionKind.AlreadyInDesiredState : MylistSyncActionKind.ExportWatchedState,
            exportDate,
            updateStates ? localState : null
        ));
        if (planOnly)
            return modifiedItems;

        await scheduler.Enqueue<UpdateAniDBMylistEntryJob>(a =>
        {
            identify(a);
            a.Data = new MylistUpdateData { State = updateStates ? localState : null, IsViewed = exportDate is not null, ViewedAt = exportDate };
            a.UpdateSeriesStats = false;
        });

        return modifiedItems;
    }

    private Task<int> ProcessStates(
        JMMUser? anidbUser,
        VideoLocal video,
        MylistEntry myItem,
        int modifiedItems,
        ISet<AnimeSeries> modifiedSeries,
        MylistState localState,
        bool readWatched,
        bool readUnwatched,
        bool setWatched,
        bool setUnwatched,
        MylistWatchedSyncMode watchedSyncMode,
        bool updateStates,
        bool planOnly,
        List<MylistSyncAction> actions
    )
    {
        var userData = anidbUser is null ? null : videoLocalUsers.GetByUserAndVideoLocalID(anidbUser.JMMUserID, video.VideoLocalID);
        var localWatchedDate = AniDBExtensions.TruncateToAniDBPrecision(userData?.WatchedDate?.ToUniversalTime());

        return ReconcileEntry(
            myItem, localWatchedDate, modifiedItems, localState,
            readWatched, readUnwatched, setWatched, setUnwatched, watchedSyncMode, updateStates,
            canImport: anidbUser is not null,
            planOnly: planOnly,
            actions: actions,
            describe: (kind, date, state) => new MylistSyncAction
            {
                Kind = kind,
                Description = kind switch
                {
                    MylistSyncActionKind.ImportWatchedState => $"Import watched state onto file {video.VideoLocalID}",
                    MylistSyncActionKind.AlreadyInDesiredState => $"The MyList entry for file {video.VideoLocalID} already reads as intended",
                    _ => $"Update the MyList entry for file {video.VideoLocalID}",
                },
                Video = video,
                VideoUserData = userData,
                Entry = myItem,
                WatchedAt = date,
                State = state,
            },
            import: async date =>
            {
                await userDataService.ImportVideoUserData(video, anidbUser!, new()
                {
                    ProgressPosition = TimeSpan.Zero,
                    LastPlayedAt = date,
                    LastUpdatedAt = myItem.UpdatedAt.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                }, "AniDB", false).ConfigureAwait(false);
                video.AnimeEpisodes
                    .DistinctBy(a => a.AnimeSeriesID)
                    .Select(a => a.AnimeSeries)
                    .WhereNotNull()
                    .ForEach(a => modifiedSeries.Add(a));
            },
            identify: a => a.VideoID = video.VideoLocalID
        );
    }

    private Task<int> ProcessGenericEntry(
        JMMUser? anidbUser,
        AnimeEpisode episode,
        MylistEntry myItem,
        int modifiedItems,
        ISet<AnimeSeries> modifiedSeries,
        MylistState localState,
        bool readWatched,
        bool readUnwatched,
        bool setWatched,
        bool setUnwatched,
        MylistWatchedSyncMode watchedSyncMode,
        bool updateStates,
        bool planOnly,
        List<MylistSyncAction> actions
    )
    {
        // the same reconciliation as files, but at the episode level
        if (episode.AniDB_Episode is not { } anidbEpisode)
            return Task.FromResult(modifiedItems);

        var userData = anidbUser is null ? null : episode.GetUserRecord(anidbUser.JMMUserID);
        var localWatchedDate = LocalWatchedDate(anidbUser, episode);

        return ReconcileEntry(
            myItem, localWatchedDate, modifiedItems, localState,
            readWatched, readUnwatched, setWatched, setUnwatched, watchedSyncMode, updateStates,
            canImport: anidbUser is not null,
            planOnly: planOnly,
            actions: actions,
            describe: (kind, date, state) => new MylistSyncAction
            {
                Kind = kind,
                Description = kind switch
                {
                    MylistSyncActionKind.ImportWatchedState => $"Import watched state onto episode {episode.AnimeEpisodeID}",
                    MylistSyncActionKind.AlreadyInDesiredState => $"The generic MyList entry for episode {episode.AnimeEpisodeID} already reads as intended",
                    _ => $"Update the generic MyList entry for episode {episode.AnimeEpisodeID}",
                },
                ShokoEpisode = episode,
                AnidbEpisode = anidbEpisode,
                EpisodeUserData = userData,
                Entry = myItem,
                WatchedAt = date,
                State = state,
            },
            import: async date =>
            {
                await userDataService.ImportEpisodeUserData(episode, anidbUser!, new()
                {
                    LastPlayedAt = date,
                    LastUpdatedAt = myItem.UpdatedAt.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                }, "AniDB", VideoUserDataSaveReason.None, false).ConfigureAwait(false);
                if (episode.AnimeSeries is { } series) modifiedSeries.Add(series);
            },
            identify: a =>
            {
                a.AnimeID = anidbEpisode.AnimeID;
                a.EpisodeType = anidbEpisode.EpisodeType;
                a.EpisodeNumber = anidbEpisode.EpisodeNumber;
            }
        );
    }


    /// <summary>
    /// The videos a scoped sync is confined to, resolved once up front. A file
    /// entry is covered when it maps to one of the videos; a generic entry is
    /// covered when it maps to an episode one of them is linked to, since that
    /// is the only entry AniDB has for a manual link.
    /// </summary>
    private sealed class SyncScope
    {
        public required IReadOnlySet<int> VideoIDs { get; init; }

        public required IReadOnlySet<int> AnidbEpisodeIDs { get; init; }

        public bool IsEmpty => VideoIDs.Count is 0 && AnidbEpisodeIDs.Count is 0;

        public bool Covers(AnimeEpisode? episode, VideoLocal? video)
            => episode is not null
                ? AnidbEpisodeIDs.Contains(episode.AniDB_EpisodeID)
                : video is not null && VideoIDs.Contains(video.VideoLocalID);
    }

    /// <summary>
    /// Resolves the episodes into a scope, dropping any that are not local. The
    /// videos linked to them come along, so a scoped sync covering both tiers
    /// reconciles the episode's file entries as well as its generic one.
    /// </summary>
    private SyncScope BuildSyncScope(IEnumerable<IShokoEpisode> episodes)
    {
        var resolved = episodes.Select(episode => animeEpisodes.GetByID(episode.ID)).WhereNotNull()?.DistinctBy(episode => episode.AnimeEpisodeID).ToList() ?? [];
        return new SyncScope
        {
            AnidbEpisodeIDs = resolved.Select(episode => episode.AniDB_EpisodeID).Where(id => id > 0).ToHashSet(),
            VideoIDs = resolved.SelectMany(episode => episode.VideoLocals).Select(video => video.VideoLocalID).ToHashSet(),
        };
    }

    /// <summary>
    /// Resolves the videos into a scope, dropping any that are not local.
    /// </summary>
    private SyncScope BuildSyncScope(IEnumerable<IVideo> videos)
    {
        var resolved = videos.Select(video => videoLocals.GetByID(video.ID)).WhereNotNull().DistinctBy(video => video.VideoLocalID).ToList();
        return new SyncScope
        {
            VideoIDs = resolved.Select(video => video.VideoLocalID).ToHashSet(),
            AnidbEpisodeIDs = resolved.SelectMany(video => video.EpisodeCrossReferences).Select(xref => xref.EpisodeID).Where(id => id > 0).ToHashSet(),
        };
    }

    /// <summary>
    /// The date the AniDB user watched the episode locally, at the precision
    /// AniDB can carry.
    /// </summary>
    private static DateTime? LocalWatchedDate(JMMUser? anidbUser, AnimeEpisode episode)
        => anidbUser is null ? null : AniDBExtensions.TruncateToAniDBPrecision(episode.GetUserRecord(anidbUser.JMMUserID)?.WatchedDate?.ToUniversalTime());

    /// <summary>
    /// Creates the generic entries the MyList is missing. The remote-driven loop
    /// above can only reconcile entries AniDB already holds, so an episode the
    /// user watched without a file — or holds only through a manual link, which
    /// has no file entry of its own — is invisible to it and is picked up here.
    /// </summary>
    private async Task<int> AddMissingEpisodes(IReadOnlyList<MylistEntry> entries, SyncScope? scope, JMMUser? anidbUser, MylistWatchedEpisodeMode watchedEpisodeMode, bool planOnly, List<MylistSyncAction> actions)
    {
        if (!settingsProvider.GetSettings().AniDb.MyList_AddFiles)
            return 0;

        // a generic entry already present is the remote-driven loop's business
        var coveredEpisodeIDs = entries.Where(entry => entry.IsGeneric is true && entry.EpisodeID is not 0).Select(entry => entry.EpisodeID).ToHashSet();
        var fileEntriesByEpisode = entries.Where(entry => entry.IsGeneric is not true && entry.EpisodeID is not 0).ToLookup(entry => entry.EpisodeID);

        var added = 0;
        foreach (var episode in ResolveSweepCandidates(scope))
        {
            if (episode.AniDB_Episode is not { } anidbEpisode || coveredEpisodeIDs.Contains(episode.AniDB_EpisodeID))
                continue;

            var videos = episode.VideoLocals;

            // a file with an AniDB release has an entry of its own, so the file
            // tier owns the episode and there is no generic entry to want
            if (videos.Any(video => video.ReleaseInfo?.ReleaseURI?.StartsWith(AnidbReleaseProvider.ReleasePrefix) ?? false))
                continue;

            var watchedDate = LocalWatchedDate(anidbUser, episode);

            MylistSyncAction Describe(MylistSyncActionKind kind, string description) => new()
            {
                Kind = kind,
                Description = description,
                ShokoEpisode = episode,
                AnidbEpisode = anidbEpisode,
                EpisodeUserData = anidbUser is null ? null : episode.GetUserRecord(anidbUser.JMMUserID),
                WatchedAt = watchedDate,
            };

            // a manual link is covered by the episode's generic entry and nothing
            // else, so it wants one whether or not the episode has been watched
            if (videos.Count > 0)
            {
                added++;
                actions.Add(Describe(MylistSyncActionKind.ExportEntryAddition, $"Add a generic MyList entry for manually linked episode {episode.AnimeEpisodeID}"));
                if (!planOnly)
                    await ScheduleAddEntry(anidbEpisode.AnimeID, anidbEpisode.EpisodeType, anidbEpisode.EpisodeNumber, WatchedData(watchedDate));
                continue;
            }

            // nothing local backs the episode, so the only reason to record it is
            // that the user watched it
            if (watchedDate is null)
                continue;

            var fileEntries = fileEntriesByEpisode[episode.AniDB_EpisodeID].ToList();
            switch (watchedEpisodeMode)
            {
                case MylistWatchedEpisodeMode.Ignore:
                    continue;

                // attach to the oldest entry AniDB already holds rather than adding
                // a second one covering the same episode; with none to attach to,
                // a generic entry is the only way to record the watch
                case MylistWatchedEpisodeMode.AttachToOldest when fileEntries.Count > 0:
                    var oldest = fileEntries
                        .OrderBy(entry => entry.MylistID is 0 ? ulong.MaxValue : entry.MylistID)
                        .ThenBy(entry => entry.FileID is 0 ? int.MaxValue : entry.FileID)
                        .First();
                    added++;
                    actions.Add(Describe(MylistSyncActionKind.ExportWatchedState, $"Record the watch for episode {episode.AnimeEpisodeID} on its oldest existing MyList entry") with
                    {
                        Entry = oldest,
                    });
                    if (planOnly)
                        continue;

                    if (oldest.MylistID is not 0)
                        await ScheduleUpdateEntry(oldest.MylistID, new() { IsViewed = true, ViewedAt = watchedDate });
                    else
                        await ScheduleUpdateEntry(oldest.FileID, new() { IsViewed = true, ViewedAt = watchedDate });
                    continue;

                default:
                    added++;
                    actions.Add(Describe(MylistSyncActionKind.ExportEntryAddition, $"Add a generic MyList entry recording the watch for episode {episode.AnimeEpisodeID}"));
                    if (!planOnly)
                        await ScheduleAddEntry(anidbEpisode.AnimeID, anidbEpisode.EpisodeType, anidbEpisode.EpisodeNumber, WatchedData(watchedDate));
                    continue;
            }
        }

        return added;

        static MylistAddData? WatchedData(DateTime? watchedDate)
            => watchedDate is null ? null : new MylistAddData { IsViewed = true, ViewedAt = watchedDate };
    }

    /// <summary>
    /// The episodes the sweep considers: the scoped ones, or — unscoped —
    /// everything the user has watched plus everything held by a manual link.
    /// </summary>
    private IEnumerable<AnimeEpisode> ResolveSweepCandidates(SyncScope? scope)
    {
        if (scope is not null)
            return scope.AnidbEpisodeIDs.Select(animeEpisodes.GetByAniDBEpisodeID).WhereNotNull() ?? [];

        var watched = animeEpisodeUsers.GetAll()
            .Where(record => record.WatchedDate.HasValue)
            .Select(record => animeEpisodes.GetByID(record.AnimeEpisodeID))
            .WhereNotNull() ?? [];
        var manuallyLinked = videoLocals.GetManuallyLinkedVideos().SelectMany(video => video.AnimeEpisodes);
        return watched.Concat(manuallyLinked).DistinctBy(episode => episode.AnimeEpisodeID);
    }

    private async Task<int> AddMissingFiles(
        ILookup<string, StoredReleaseInfo> localFiles,
        ILookup<int, MylistEntry> onlineFiles,
        SyncScope? scope,
        bool planOnly,
        List<MylistSyncAction> actions
    )
    {
        if (!settingsProvider.GetSettings().AniDb.MyList_AddFiles)
            return 0;
        var missingFiles = 0;
        var candidates = scope is null
            ? videoLocals.GetAll()
            : scope.VideoIDs.Select(videoLocals.GetByID).WhereNotNull();
        foreach (var vid in candidates.Where(a => !string.IsNullOrEmpty(a.Hash)))
        {
            if (!TryGetFileID(localFiles, vid.Hash, out var fileID)) continue;
            // the file is in the local collection but not recorded online
            if (onlineFiles.Contains(fileID)) continue;
            missingFiles++;
            actions.Add(new MylistSyncAction
            {
                Kind = MylistSyncActionKind.ExportEntryAddition,
                Description = $"Add file {vid.VideoLocalID} to the MyList",
                Video = vid,
            });
            if (planOnly)
                continue;

            await scheduler.Enqueue<AddAniDBMylistEntryJob>(a =>
            {
                a.ED2K = vid.Hash;
                a.FileSize = vid.FileSize;
            });
        }

        _logger.LogInformation(
            "MYLIST Missing Files: {MissingFiles} Added to queue for inclusion",
            missingFiles);
        return missingFiles;
    }

    private static bool TryGetFileID(ILookup<string, StoredReleaseInfo> localFiles, string hash, out int fileID)
    {
        fileID = 0;
        return localFiles[hash].FirstOrDefault() is { ReleaseURI: { } uri }
            && int.TryParse(uri.AsSpan(AnidbReleaseProvider.ReleasePrefix.Length), out fileID)
            && fileID != 0;
    }

    #endregion

    #endregion
}
