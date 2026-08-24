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
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Enums;
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
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

/// <summary>
/// Service for interacting with the AniDB MyList. All MyList operations,
/// whether immediate or scheduled through the job queue, should be routed
/// through this service.
/// </summary>
public class MyListService(
    ILogger<MyListService> logger,
    IRequestFactory requestFactory,
    IQueueScheduler scheduler,
    ISettingsProvider settingsProvider,
    IApplicationPaths applicationPaths,
    IUserDataService userDataService,
    MyListCache mylistCache,
    MyListGenericsCache genericsCache,
    JMMUserRepository users,
    VideoLocalRepository videoLocals,
    VideoLocal_UserRepository videoLocalUsers,
    AnimeEpisodeRepository animeEpisodes,
    AniDB_EpisodeRepository anidbEpisodes,
    StoredReleaseInfoRepository storedReleaseInfos,
    AnimeSeriesService seriesService
) : IMyListService
{
    /// <summary>
    /// How long the locally cached MyList is considered fresh enough to serve
    /// without going back to AniDB over HTTP. Deliberately independent of
    /// <c>MyList_UpdateFrequency</c>, which schedules the sync rather than
    /// bounding the cache; callers that need a guaranteed-current entry pass
    /// <see cref="MyListFetchMode.IgnoreTimeCheck"/> instead.
    /// </summary>
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(12);

    private readonly ILogger<MyListService> _logger = logger;

    /// <summary>
    /// Guards <see cref="SyncAsync"/> against overlapping runs. The queue job
    /// is already <c>[DisallowConcurrentExecution]</c>, but the method is on the
    /// plugin-facing contract and can be called without going through it.
    /// </summary>
    internal readonly SemaphoreSlim _syncLock = new(1, 1);

    public MyListFetchMode FetchMode
    {
        get => settingsProvider.GetSettings().AniDb.MyList_FetchMode;
        set
        {
            if (value is MyListFetchMode.Auto or MyListFetchMode.None)
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
    private const MyListFetchMode TransportFlags = MyListFetchMode.Http | MyListFetchMode.Udp | MyListFetchMode.Cache;

    /// <summary>
    /// Resolves <see cref="MyListFetchMode.Auto"/> to the configured mode, and
    /// fills the configured transports in when the caller passed modifier flags
    /// only. <see cref="MyListFetchMode.IgnoreTimeCheck"/> on its own carries no
    /// transport, so taking it at face value would fetch nothing at all.
    /// <see cref="MyListFetchMode.None"/> stays as it is; it means do nothing.
    /// </summary>
    internal MyListFetchMode ResolveFetchMode(MyListFetchMode fetchMode)
    {
        if (fetchMode is MyListFetchMode.Auto)
            return settingsProvider.GetSettings().AniDb.MyList_FetchMode;

        if (fetchMode is MyListFetchMode.None || (fetchMode & TransportFlags) is not MyListFetchMode.None)
            return fetchMode;

        return settingsProvider.GetSettings().AniDb.MyList_FetchMode | fetchMode;
    }

    private MyListReadStates ResolveReadStates(MyListReadStates readStates)
    {
        if (readStates is not MyListReadStates.Auto)
            return readStates;

        var settings = settingsProvider.GetSettings();
        var resolved = MyListReadStates.None;
        if (settings.AniDb.MyList_ReadWatched) resolved |= MyListReadStates.Watched;
        if (settings.AniDb.MyList_ReadUnwatched) resolved |= MyListReadStates.Unwatched;
        return resolved;
    }

    #region Fetch

    public async Task<IReadOnlyList<MyListEntry>> GetEntriesAsync(MyListFetchMode fetchMode = MyListFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MyListFetchMode.None)
            return [];

        // serve the cache when allowed and fresh, or when the time check is ignored
        if (fetchMode.HasFlag(MyListFetchMode.Cache))
        {
            var cached = mylistCache.GetAll();
            if (cached.Count > 0 && (fetchMode.HasFlag(MyListFetchMode.IgnoreTimeCheck) || IsCacheFresh()))
                return cached;
        }

        if (fetchMode.HasFlag(MyListFetchMode.Http))
        {
            try
            {
                return await FetchMyListAsync(cancellationToken);
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

    public Task<MyListEntry?> GetEntryAsync(ulong listID, MyListFetchMode fetchMode = MyListFetchMode.Auto, CancellationToken cancellationToken = default)
        => GetEntryInternalAsync(fetchMode, () => mylistCache.GetByLid(listID), r => r.MyListID = listID, cancellationToken: cancellationToken);

    public Task<MyListEntry?> GetEntryAsync(string ed2k, long fileSize, MyListFetchMode fetchMode = MyListFetchMode.Auto, CancellationToken cancellationToken = default)
        => GetEntryInternalAsync(fetchMode, () => mylistCache.GetByEd2k(ed2k, fileSize), r =>
        {
            r.ED2K = ed2k;
            r.Size = fileSize;
        }, entry => entry with { ED2K = ed2k, Size = fileSize, IsGeneric = false }, cancellationToken);

    public Task<MyListEntry?> GetEntryAsync(int fileID, MyListFetchMode fetchMode = MyListFetchMode.Auto, CancellationToken cancellationToken = default)
        => GetEntryInternalAsync(fetchMode, () => mylistCache.GetByFileID(fileID), r => r.FileID = (ulong)fileID, cancellationToken: cancellationToken);

    public Task<MyListEntry?> GetEntryAsync(int animeID, EpisodeType episodeType, int episodeNumber, MyListFetchMode fetchMode = MyListFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        var aniDbEpisode = anidbEpisodes.GetByAnimeIDAndEpisodeTypeNumber(animeID, episodeType, episodeNumber).FirstOrDefault();
        return GetEntryInternalAsync(fetchMode, () => aniDbEpisode is null ? null : mylistCache.GetByEpisodeID(aniDbEpisode.EpisodeID), r =>
        {
            r.AnimeID = animeID;
            r.EpisodeType = episodeType;
            r.EpisodeNumber = episodeNumber;
        }, entry => aniDbEpisode is null ? entry : entry with { AnimeID = animeID, EpisodeID = aniDbEpisode.EpisodeID, IsGeneric = true }, cancellationToken);
    }

    public async Task<IReadOnlyList<MyListEntry>> GetEntriesForVideoAsync(IVideo video, MyListFetchMode fetchMode = MyListFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        var vid = videoLocals.GetByID(video.ID);
        if (vid is null)
            return [];

        var entries = new List<MyListEntry>();
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
    private async Task<MyListEntry?> GetEntryInternalAsync(
        MyListFetchMode fetchMode,
        Func<MyListEntry?> cacheLookup,
        Action<RequestGetMyList> configureRequest,
        Func<MyListEntry, MyListEntry>? enrichEntry = null,
        CancellationToken cancellationToken = default
    )
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MyListFetchMode.None)
            return null;

        // 1. HTTP — gated by the time check unless ignored
        var httpSucceeded = false;
        if (fetchMode.HasFlag(MyListFetchMode.Http) && (fetchMode.HasFlag(MyListFetchMode.IgnoreTimeCheck) || !IsCacheFresh()))
        {
            try
            {
                await FetchMyListAsync(cancellationToken);
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
        if (fetchMode.HasFlag(MyListFetchMode.Cache) && cacheLookup() is { } cached)
            return cached;

        // 3. UDP — only when HTTP was not used successfully
        if (fetchMode.HasFlag(MyListFetchMode.Udp) && !httpSucceeded)
        {
            try
            {
                var request = requestFactory.Create<RequestGetMyList>(configureRequest);
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
    private async Task<IReadOnlyList<MyListEntry>> FetchMyListAsync(CancellationToken cancellationToken)
    {
        var settings = settingsProvider.GetSettings();
        if (settings.AniDb.MyList_UseGenericFileIndex)
            await genericsCache.EnsureLoadedAsync(cancellationToken);
        var request = requestFactory.Create<RequestMyList>(
            r =>
            {
                r.Username = settings.AniDb.Username!;
                r.Password = settings.AniDb.Password!;
            }
        );
        var response = request.Send();

        if (response.Response is null)
            throw new Exception($"AniDB did not return a successful code: {response.Code}");

        var entries = EnrichEntries(response.Response);
        mylistCache.ReplaceAll(entries);
        await CreateEntriesBackup(entries, settings);
        return entries;
    }

    /// <summary>
    /// Refreshes the cache over HTTP when the fetch mode allows it and the
    /// cache is stale, or the time check is ignored. Failures are logged
    /// and swallowed, so the caller can continue with the cache and UDP.
    /// </summary>
    private async Task RefreshCacheIfAllowedAsync(MyListFetchMode fetchMode, CancellationToken cancellationToken)
    {
        if (!fetchMode.HasFlag(MyListFetchMode.Http)) return;
        if (!fetchMode.HasFlag(MyListFetchMode.IgnoreTimeCheck) && IsCacheFresh()) return;

        try
        {
            await FetchMyListAsync(cancellationToken);
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

    private async Task CreateEntriesBackup(IReadOnlyList<MyListEntry> entries, IServerSettings settings)
    {
        // separate directory so a backup can never collide with the working cache
        var backupDirectory = new DirectoryInfo(Path.Combine(applicationPaths.DataPath, "MyList", "Backups"));
        backupDirectory.Create();

        // rotation sorts on the filename, so the timestamp has to be universally
        // sortable ("u" format specifier)
        var backupPath = Path.Join(backupDirectory.FullName, DateTimeOffset.UtcNow.ToString("u").Replace(':', '_') + ".json.gz");
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

        // only files whose name starts with an ISO 8601 date, so nothing else in
        // the directory is ever a rotation candidate
        var backupFiles = backupDirectory.GetFiles("????-??-?? *.json.gz").OrderByDescending(f => f.Name).ToList();
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

    private IReadOnlyList<MyListEntry> EnrichEntries(IEnumerable<MyListEntry> entries)
    {
        // the export says nothing about which entries are generic, so resolve it
        // from the index when we have one and leave it unknown when we do not
        var useGenericsIndex = settingsProvider.GetSettings().AniDb.MyList_UseGenericFileIndex && genericsCache.IsAvailable;
        var enriched = new List<MyListEntry>();
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

    public async Task<MyListEntry?> AddEntryAsync(int fileID, MyListAddData? data = null, MyListFetchMode fetchMode = MyListFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MyListFetchMode.None)
            return mylistCache.GetByFileID(fileID);

        _logger.LogInformation("Adding a MyList entry. (FileID={FileID})", fileID);

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        data = ResolveAddData(data);

        // short-circuit when the cached entry is already in the desired state
        if (fetchMode.HasFlag(MyListFetchMode.Cache) && mylistCache.GetByFileID(fileID) is { } cachedEntry &&
            MyListCache.IsInDesiredState(cachedEntry, data))
        {
            _logger.LogInformation("Skipping the MyList entry add; it is already in the desired state. (FileID={FileID}, MyListID={MyListID})", fileID, cachedEntry.MyListID);
            return cachedEntry;
        }

        MyListEntry? myListEntry;
        var request = requestFactory.Create<RequestAddMyList>(
            r =>
            {
                ApplyAddData(r, data);
                r.FileID = (ulong)fileID;
            }
        );
        var response = request.Send();
        MyListEntry? patched = null;

        if (response.Code == UDPReturnCode.FILE_ALREADY_IN_MYLIST)
        {
            var updateRequest = requestFactory.Create<RequestUpdateMyList>(
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
        myListEntry = patched ?? response.Response;
        if (myListEntry is not null) mylistCache.Upsert(myListEntry);

        return myListEntry;
    }

    public Task ScheduleAddEntry(int fileID, MyListAddData? data = null, MyListFetchMode fetchMode = MyListFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<AddAniDBMyListEntryJob>(a =>
        {
            a.FileID = fileID;
            a.Data = data;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<MyListEntry?> AddEntryAsync(string ed2k, long fileSize, MyListAddData? data = null, MyListFetchMode fetchMode = MyListFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MyListFetchMode.None)
            return mylistCache.GetByEd2k(ed2k, fileSize);

        _logger.LogInformation("Adding a MyList entry. (ED2K={Hash}, Size={Size})", ed2k, fileSize);

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        data = ResolveAddData(data);

        // short-circuit when the cached entry is already in the desired state
        if (fetchMode.HasFlag(MyListFetchMode.Cache) && mylistCache.GetByEd2k(ed2k, fileSize) is { } cachedEntry &&
            MyListCache.IsInDesiredState(cachedEntry, data))
        {
            _logger.LogInformation("Skipping the MyList entry add; it is already in the desired state. (ED2K={Hash}, Size={Size}, MyListID={MyListID})", ed2k, fileSize, cachedEntry.MyListID);
            return cachedEntry;
        }

        MyListEntry? myListEntry;
        var request = requestFactory.Create<RequestAddMyList>(
            r =>
            {
                ApplyAddData(r, data);
                r.ED2K = ed2k;
                r.Size = fileSize;
            }
        );
        var response = request.Send();
        MyListEntry? patched = null;

        if (response.Code == UDPReturnCode.FILE_ALREADY_IN_MYLIST)
        {
            var updateRequest = requestFactory.Create<RequestUpdateMyList>(
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
        myListEntry = patched ?? response.Response;
        if (myListEntry is not null) mylistCache.Upsert(myListEntry with { ED2K = ed2k, Size = fileSize, IsGeneric = false });

        return myListEntry;
    }

    public Task ScheduleAddEntry(string ed2k, long fileSize, MyListAddData? data = null, MyListFetchMode fetchMode = MyListFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<AddAniDBMyListEntryJob>(a =>
        {
            a.ED2K = ed2k;
            a.FileSize = fileSize;
            a.Data = data;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<MyListEntry?> AddEntryAsync(int animeID, EpisodeType episodeType, int episodeNumber, MyListAddData? data = null, MyListFetchMode fetchMode = MyListFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MyListFetchMode.None)
        {
            var noneEpisode = anidbEpisodes.GetByAnimeIDAndEpisodeTypeNumber(animeID, episodeType, episodeNumber).FirstOrDefault();
            return noneEpisode is null ? null : mylistCache.GetByEpisodeID(noneEpisode.EpisodeID);
        }

        _logger.LogInformation("Adding a MyList entry. (AnimeID={AnimeID}, EpisodeType={EpisodeType}, EpisodeNumber={EpisodeNumber})", animeID, episodeType, episodeNumber);

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        data = ResolveAddData(data);

        // short-circuit when the cached entry is already in the desired state
        var aniDbEpisode = anidbEpisodes.GetByAnimeIDAndEpisodeTypeNumber(animeID, episodeType, episodeNumber).FirstOrDefault();
        if (fetchMode.HasFlag(MyListFetchMode.Cache) && aniDbEpisode is not null && mylistCache.GetByEpisodeID(aniDbEpisode.EpisodeID) is { } cachedEntry &&
            MyListCache.IsInDesiredState(cachedEntry, data))
        {
            _logger.LogInformation("Skipping the MyList entry add; it is already in the desired state. (AnimeID={AnimeID}, EpisodeType={EpisodeType}, EpisodeNumber={EpisodeNumber}, MyListID={MyListID})", animeID, episodeType, episodeNumber, cachedEntry.MyListID);
            return cachedEntry;
        }

        var request = requestFactory.Create<RequestAddMyList>(
            r =>
            {
                ApplyAddData(r, data);
                r.AnimeID = animeID;
                r.EpisodeNumber = episodeNumber;
                r.EpisodeType = episodeType;
            }
        );
        var response = request.Send();
        MyListEntry? patched = null;

        if (response.Code == UDPReturnCode.FILE_ALREADY_IN_MYLIST)
        {
            var updateRequest = requestFactory.Create<RequestUpdateMyList>(
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
        var myListEntry = patched ?? response.Response;
        if (myListEntry is not null && aniDbEpisode is not null)
            myListEntry = myListEntry with { AnimeID = animeID, EpisodeID = aniDbEpisode.EpisodeID, IsGeneric = true };
        if (myListEntry is not null) mylistCache.Upsert(myListEntry);

        return myListEntry;
    }

    public Task ScheduleAddEntry(int animeID, EpisodeType episodeType, int episodeNumber, MyListAddData? data = null, MyListFetchMode fetchMode = MyListFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<AddAniDBMyListEntryJob>(a =>
        {
            a.AnimeID = animeID;
            a.EpisodeType = episodeType;
            a.EpisodeNumber = episodeNumber;
            a.Data = data;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<MyListEntry?> AddVideoAsync(IVideo video, MyListAddData? data = null, MyListReadStates readStates = MyListReadStates.Auto, MyListFetchMode fetchMode = MyListFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        if (video is not VideoLocal videoLocal)
            throw new ArgumentException("Video must be a VideoLocal object", nameof(video));

        readStates = ResolveReadStates(readStates);
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MyListFetchMode.None)
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

        MyListEntry? myListEntry;
        MyListAddData resolvedData;
        if (isManualLink)
        {
            // a manual link has no file of its own on AniDB, so it is added as a
            // generic entry for each linked episode instead
            resolvedData = ResolveAddData(data);
            myListEntry = null;
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

                myListEntry = episodeEntry;
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
            myListEntry = await AddEntryAsync(videoLocal.Hash, videoLocal.FileSize, resolvedData, fetchMode, cancellationToken).ConfigureAwait(false);
            if (myListEntry is not null && user is not null)
                await ImportWatchedState(myListEntry, readStates, originalWatchedDate, updateDate => userDataService.ImportVideoUserData(videoLocal, user, new()
                {
                    ProgressPosition = TimeSpan.Zero,
                    LastPlayedAt = updateDate,
                    LastUpdatedAt = myListEntry.UpdatedAt.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                }, "AniDB")).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Added the MyList entry for a video. (File={FileName}, ManualLink={IsManualLink}, WatchedLocally={WatchedLocally}, WatchedOnAniDB={WatchedOnAniDB}, DesiredState={DesiredState}, AniDbState={AniDbState}, ReadStates={ReadStates})",
            videoLocal.FirstValidPlace?.FileName, isManualLink, originalWatchedDate != null,
            myListEntry?.IsViewed, resolvedData.State, myListEntry?.State, readStates
        );

        var series = videoLocal.EpisodeCrossReferences.Select(a => a.AnimeID).Distinct().Except([0]).ToArray();
        if (series.Length > 0)
            await Task.WhenAll(series.Select(id => scheduler.Enqueue<RefreshAnimeStatsJob>(a => a.AnimeID = id)));

        return myListEntry;
    }

    public Task ScheduleAddVideo(IVideo video, MyListAddData? data = null, MyListReadStates readStates = MyListReadStates.Auto, MyListFetchMode fetchMode = MyListFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<AddAniDBMyListEntryJob>(a =>
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
    private static async Task ImportWatchedState(MyListEntry entry, MyListReadStates readStates, DateTime? localWatchedDate, Func<DateTime?, Task> import)
    {
        var watched = entry.ViewedAt is { } viewedAt && !DateTime.UnixEpoch.Equals(viewedAt);
        var watchedLocally = localWatchedDate is not null;
        if (readStates.HasFlag(MyListReadStates.Watched) && watched && !watchedLocally)
            await import(entry.ViewedAt ?? DateTime.Now).ConfigureAwait(false);
        else if (readStates.HasFlag(MyListReadStates.Unwatched) && !watched && watchedLocally)
            await import(null).ConfigureAwait(false);
    }

    /// <summary>
    /// The file state a request should send, or <c>null</c> to leave it alone.
    /// AniDB rejects the whole command with a 505 for a state its UDP validator
    /// does not know, so an unwritable one is dropped instead of taking the
    /// rest of the request down with it.
    /// </summary>
    private MyListFileState? ResolveFileState(MyListFileState? fileState)
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
    /// state is deliberately left alone; see <see cref="MyListFileState"/>.
    /// </summary>
    private MyListAddData ResolveAddData(MyListAddData? data, DateTime? fallbackWatchedDate = null)
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

    private void ApplyAddData(RequestAddMyList r, MyListAddData data)
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

    public async Task<MyListEntry?> UpdateEntryAsync(ulong listID, MyListUpdateData data, MyListFetchMode fetchMode = MyListFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MyListFetchMode.None)
            return mylistCache.GetByLid(listID);

        _logger.LogInformation("Updating a MyList entry. (MyListID={MyListID})", listID);

        // an update data with no fields set is a no-op
        if (data.IsEmpty)
        {
            _logger.LogInformation("Skipping the MyList entry update; no fields were set. (MyListID={MyListID})", listID);
            return mylistCache.GetByLid(listID);
        }

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        // short-circuit when the cached entry is already in the desired state
        var cachedEntry = mylistCache.GetByLid(listID);
        if (fetchMode.HasFlag(MyListFetchMode.Cache) && cachedEntry is not null &&
            MyListCache.IsInDesiredState(cachedEntry, data))
        {
            _logger.LogInformation("Skipping the MyList entry update; it is already in the desired state. (MyListID={MyListID})", listID);
            return cachedEntry;
        }

        _logger.LogInformation("Sending the MyList entry update. (MyListID={MyListID})", listID);
        var request = requestFactory.Create<RequestUpdateMyList>(
            r =>
            {
                r.MyListID = listID;
                ApplyUpdateData(r, data);
            }
        );

        var code = request.Send().Code;
        return PersistUpdate(code, cachedEntry, data, () => requestFactory.Create<RequestGetMyList>(r => r.MyListID = listID));
    }

    public Task ScheduleUpdateEntry(ulong listID, MyListUpdateData? data = null, MyListFetchMode fetchMode = MyListFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<UpdateAniDBMyListEntryJob>(a =>
        {
            a.MyListID = listID;
            a.Data = data;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<MyListEntry?> UpdateEntryAsync(int fileID, MyListUpdateData data, MyListFetchMode fetchMode = MyListFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MyListFetchMode.None)
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
        if (fetchMode.HasFlag(MyListFetchMode.Cache) && cachedEntry is not null &&
            MyListCache.IsInDesiredState(cachedEntry, data))
        {
            _logger.LogInformation("Skipping the MyList entry update; it is already in the desired state. (MyListID={MyListID})", cachedEntry.MyListID);
            return cachedEntry;
        }

        _logger.LogInformation("Sending the MyList entry update. (FileID={FileID})", fileID);
        var request = requestFactory.Create<RequestUpdateMyList>(
            r =>
            {
                r.FileID = (ulong)fileID;
                ApplyUpdateData(r, data);
            }
        );

        var code = request.Send().Code;
        return PersistUpdate(code, cachedEntry, data, () => requestFactory.Create<RequestGetMyList>(r => r.FileID = (ulong)fileID));
    }

    public Task ScheduleUpdateEntry(int fileID, MyListUpdateData? data = null, MyListFetchMode fetchMode = MyListFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<UpdateAniDBMyListEntryJob>(a =>
        {
            a.FileID = fileID;
            a.Data = data;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<MyListEntry?> UpdateEntryAsync(string ed2k, long fileSize, MyListUpdateData data, MyListFetchMode fetchMode = MyListFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MyListFetchMode.None)
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
        if (fetchMode.HasFlag(MyListFetchMode.Cache) && cachedEntry is not null &&
            MyListCache.IsInDesiredState(cachedEntry, data))
        {
            _logger.LogInformation("Skipping the MyList entry update; it is already in the desired state. (MyListID={MyListID})", cachedEntry.MyListID);
            return cachedEntry;
        }

        _logger.LogInformation("Sending the MyList entry update. (ED2K={Hash}, Size={Size})", ed2k, fileSize);
        var request = requestFactory.Create<RequestUpdateMyList>(
            r =>
            {
                r.ED2K = ed2k;
                r.Size = fileSize;
                ApplyUpdateData(r, data);
            }
        );

        var code = request.Send().Code;
        return PersistUpdate(code, cachedEntry, data, () => requestFactory.Create<RequestGetMyList>(r =>
        {
            r.ED2K = ed2k;
            r.Size = fileSize;
        }), entry => entry with { ED2K = ed2k, Size = fileSize, IsGeneric = false });
    }

    public Task ScheduleUpdateEntry(string ed2k, long fileSize, MyListUpdateData? data = null, MyListFetchMode fetchMode = MyListFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<UpdateAniDBMyListEntryJob>(a =>
        {
            a.ED2K = ed2k;
            a.FileSize = fileSize;
            a.Data = data;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<MyListEntry?> UpdateEntryAsync(int animeID, EpisodeType episodeType, int episodeNumber, MyListUpdateData data, MyListFetchMode fetchMode = MyListFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MyListFetchMode.None)
        {
            var noneEpisode = anidbEpisodes.GetByAnimeIDAndEpisodeTypeNumber(animeID, episodeType, episodeNumber).FirstOrDefault();
            return noneEpisode is null ? null : mylistCache.GetByEpisodeID(noneEpisode.EpisodeID);
        }

        _logger.LogInformation("Updating a MyList entry. (AnimeID={AnimeID}, EpisodeType={EpisodeType}, EpisodeNumber={EpisodeNumber})", animeID, episodeType, episodeNumber);

        var aniDbEpisode = anidbEpisodes.GetByAnimeIDAndEpisodeTypeNumber(animeID, episodeType, episodeNumber).FirstOrDefault();

        // an update data with no fields set is a no-op
        if (data.IsEmpty)
        {
            _logger.LogInformation("Skipping the MyList entry update; no fields were set. (AnimeID={AnimeID}, EpisodeType={EpisodeType}, EpisodeNumber={EpisodeNumber})", animeID, episodeType, episodeNumber);
            return aniDbEpisode is null ? null : mylistCache.GetByEpisodeID(aniDbEpisode.EpisodeID);
        }

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        // short-circuit when the cached entry is already in the desired state
        var cachedEntry = aniDbEpisode is null ? null : mylistCache.GetByEpisodeID(aniDbEpisode.EpisodeID);
        if (fetchMode.HasFlag(MyListFetchMode.Cache) && cachedEntry is not null &&
            MyListCache.IsInDesiredState(cachedEntry, data))
        {
            _logger.LogInformation("Skipping the MyList entry update; it is already in the desired state. (MyListID={MyListID})", cachedEntry.MyListID);
            return cachedEntry;
        }

        _logger.LogInformation("Sending the MyList entry update. (AnimeID={AnimeID}, EpisodeType={EpisodeType}, EpisodeNumber={EpisodeNumber})", animeID, episodeType, episodeNumber);
        var request = requestFactory.Create<RequestUpdateMyList>(
            r =>
            {
                r.AnimeID = animeID;
                r.EpisodeNumber = episodeNumber;
                r.EpisodeType = episodeType;
                ApplyUpdateData(r, data);
            }
        );

        var code = request.Send().Code;
        return PersistUpdate(code, cachedEntry, data, () => requestFactory.Create<RequestGetMyList>(r =>
        {
            r.AnimeID = animeID;
            r.EpisodeType = episodeType;
            r.EpisodeNumber = episodeNumber;
        }), entry => aniDbEpisode is null ? entry : entry with { AnimeID = animeID, EpisodeID = aniDbEpisode.EpisodeID, IsGeneric = true });
    }

    public Task ScheduleUpdateEntry(int animeID, EpisodeType episodeType, int episodeNumber, MyListUpdateData? data = null, MyListFetchMode fetchMode = MyListFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<UpdateAniDBMyListEntryJob>(a =>
        {
            a.AnimeID = animeID;
            a.EpisodeType = episodeType;
            a.EpisodeNumber = episodeNumber;
            a.Data = data;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<MyListEntry?> UpdateVideoAsync(IVideo video, MyListUpdateData data, bool updateSeriesStats = false, MyListFetchMode fetchMode = MyListFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating the MyList entries for a video. (VideoID={VideoID})", video.ID);

        var vid = videoLocals.GetByID(video.ID);
        if (vid == null)
            return null;

        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MyListFetchMode.None)
            return mylistCache.GetByEd2k(vid.Hash, vid.FileSize);

        // an update data with no fields set is a no-op
        if (data.IsEmpty)
        {
            _logger.LogInformation("Skipping the video MyList update; no fields were set. (VideoID={VideoID})", video.ID);
            return mylistCache.GetByEd2k(vid.Hash, vid.FileSize);
        }

        MyListEntry? entry;
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

    public Task ScheduleUpdateVideo(IVideo video, MyListUpdateData? data = null, bool updateSeriesStats = false, MyListFetchMode fetchMode = MyListFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<UpdateAniDBMyListEntryJob>(a =>
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
    private static MyListUpdateData ForEpisode(MyListUpdateData data, AnimeEpisode episode, JMMUser? user)
    {
        if (!data.IsViewed.HasValue && !data.ViewedAt.HasValue)
            return data;

        var watchedDate = user is null ? null : episode.GetUserRecord(user.JMMUserID)?.WatchedDate?.ToUniversalTime();
        return new MyListUpdateData
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
    private MyListEntry? PersistUpdate(
        UDPReturnCode code,
        MyListEntry? cached,
        MyListUpdateData data,
        Func<RequestGetMyList> createGetRequest,
        Func<MyListEntry, MyListEntry>? enrichEntry = null
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
    /// <see cref="ApplyUpdateData(RequestUpdateMyList, MyListUpdateData)"/>
    /// sends. Fields the update leaves unset keep their previous value, which
    /// is what <c>edit=1</c> does upstream.
    /// </summary>
    private MyListEntry PatchEntry(MyListEntry entry, MyListUpdateData data)
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

    private void ApplyUpdateData(RequestUpdateMyList r, MyListUpdateData data)
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

    public async Task<bool> RemoveEntryAsync(ulong listID, MyListFetchMode fetchMode = MyListFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MyListFetchMode.None)
            return false;

        _logger.LogInformation("Removing a MyList entry. (MyListID={MyListID})", listID);

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        var cached = mylistCache.GetByLid(listID);
        if (cached is null && !fetchMode.HasFlag(MyListFetchMode.Udp))
        {
            _logger.LogInformation("Skipping the MyList entry removal; it is not in the local cache and UDP is not allowed");
            return false;
        }

        var request = requestFactory.Create<RequestRemoveMyList>(r => r.MyListID = listID);
        var code = request.Send().Code;
        if (code == UDPReturnCode.MYLIST_ENTRY_DELETED && cached is not null) mylistCache.Remove(cached);
        return code == UDPReturnCode.MYLIST_ENTRY_DELETED;
    }

    public Task ScheduleRemoveEntry(ulong listID, MyListFetchMode fetchMode = MyListFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<RemoveAniDBMyListEntryJob>(a =>
        {
            a.MyListID = listID;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<bool> RemoveEntryAsync(int fileID, MyListFetchMode fetchMode = MyListFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MyListFetchMode.None)
            return false;

        _logger.LogInformation("Removing a MyList entry. (FileID={FileID})", fileID);

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        var cached = mylistCache.GetByFileID(fileID);
        if (cached is null && !fetchMode.HasFlag(MyListFetchMode.Udp))
        {
            _logger.LogInformation("Skipping the MyList entry removal; it is not in the local cache and UDP is not allowed");
            return false;
        }

        var request = requestFactory.Create<RequestRemoveMyList>(r => r.FileID = (ulong)fileID);
        var code = request.Send().Code;
        if (code == UDPReturnCode.MYLIST_ENTRY_DELETED && cached is not null) mylistCache.Remove(cached);
        return code == UDPReturnCode.MYLIST_ENTRY_DELETED;
    }

    public Task ScheduleRemoveEntry(int fileID, MyListFetchMode fetchMode = MyListFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<RemoveAniDBMyListEntryJob>(a =>
        {
            a.FileID = fileID;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<bool> RemoveEntryAsync(string ed2k, long fileSize, MyListFetchMode fetchMode = MyListFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MyListFetchMode.None)
            return false;

        _logger.LogInformation("Removing a MyList entry. (ED2K={Hash}, Size={Size})", ed2k, fileSize);

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        var cached = mylistCache.GetByEd2k(ed2k, fileSize);
        if (cached is null && !fetchMode.HasFlag(MyListFetchMode.Udp))
        {
            _logger.LogInformation("Skipping the MyList entry removal; it is not in the local cache and UDP is not allowed");
            return false;
        }

        var request = requestFactory.Create<RequestRemoveMyList>(r =>
        {
            r.ED2K = ed2k;
            r.Size = fileSize;
        });
        var code = request.Send().Code;
        if (code == UDPReturnCode.MYLIST_ENTRY_DELETED && cached is not null) mylistCache.Remove(cached);
        return code == UDPReturnCode.MYLIST_ENTRY_DELETED;
    }

    public Task ScheduleRemoveEntry(string ed2k, long fileSize, MyListFetchMode fetchMode = MyListFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<RemoveAniDBMyListEntryJob>(a =>
        {
            a.ED2K = ed2k;
            a.FileSize = fileSize;
            a.FetchMode = fetchMode;
        }, prioritize);

    public async Task<bool> RemoveEntryAsync(int animeID, EpisodeType episodeType, int episodeNumber, MyListFetchMode fetchMode = MyListFetchMode.Auto, CancellationToken cancellationToken = default)
    {
        fetchMode = ResolveFetchMode(fetchMode);
        if (fetchMode is MyListFetchMode.None)
            return false;

        _logger.LogInformation("Removing a MyList entry. (AnimeID={AnimeID}, EpisodeType={EpisodeType}, EpisodeNumber={EpisodeNumber})", animeID, episodeType, episodeNumber);

        await RefreshCacheIfAllowedAsync(fetchMode, cancellationToken);

        var episode = anidbEpisodes.GetByAnimeIDAndEpisodeTypeNumber(animeID, episodeType, episodeNumber).FirstOrDefault();
        var cached = episode is null ? null : mylistCache.GetByEpisodeID(episode.EpisodeID);
        if (cached is null && !fetchMode.HasFlag(MyListFetchMode.Udp))
        {
            _logger.LogInformation("Skipping the MyList entry removal; it is not in the local cache and UDP is not allowed");
            return false;
        }

        var request = requestFactory.Create<RequestRemoveMyList>(r =>
        {
            r.AnimeID = animeID;
            r.EpisodeType = episodeType;
            r.EpisodeNumber = episodeNumber;
        });
        var code = request.Send().Code;
        if (code == UDPReturnCode.MYLIST_ENTRY_DELETED && cached is not null) mylistCache.Remove(cached);
        return code == UDPReturnCode.MYLIST_ENTRY_DELETED;
    }

    public Task ScheduleRemoveEntry(int animeID, EpisodeType episodeType, int episodeNumber, MyListFetchMode fetchMode = MyListFetchMode.Auto, bool prioritize = false)
        => scheduler.Enqueue<RemoveAniDBMyListEntryJob>(a =>
        {
            a.AnimeID = animeID;
            a.EpisodeType = episodeType;
            a.EpisodeNumber = episodeNumber;
            a.FetchMode = fetchMode;
        }, prioritize);

    public Task ScheduleDisposeEntry(ulong listID, MyListDeleteType? deleteType = null, MyListFetchMode fetchMode = MyListFetchMode.Auto, bool prioritize = false)
        => DisposeEntry(deleteType, state => ScheduleUpdateEntry(listID, new() { State = state }, fetchMode, prioritize), () => ScheduleRemoveEntry(listID, fetchMode, prioritize));

    public Task ScheduleDisposeEntry(int fileID, MyListDeleteType? deleteType = null, MyListFetchMode fetchMode = MyListFetchMode.Auto, bool prioritize = false)
        => DisposeEntry(deleteType, state => ScheduleUpdateEntry(fileID, new() { State = state }, fetchMode, prioritize), () => ScheduleRemoveEntry(fileID, fetchMode, prioritize));

    public Task ScheduleDisposeEntry(string ed2k, long fileSize, MyListDeleteType? deleteType = null, MyListFetchMode fetchMode = MyListFetchMode.Auto, bool prioritize = false)
        => DisposeEntry(deleteType, state => ScheduleUpdateEntry(ed2k, fileSize, new() { State = state }, fetchMode, prioritize), () => ScheduleRemoveEntry(ed2k, fileSize, fetchMode, prioritize));

    public Task ScheduleDisposeEntry(int animeID, EpisodeType episodeType, int episodeNumber, MyListDeleteType? deleteType = null, MyListFetchMode fetchMode = MyListFetchMode.Auto, bool prioritize = false)
        => DisposeEntry(
            deleteType,
            state => ScheduleUpdateEntry(animeID, episodeType, episodeNumber, new() { State = state }, fetchMode, prioritize),
            () => ScheduleRemoveEntry(animeID, episodeType, episodeNumber, fetchMode, prioritize)
        );

    public async Task ScheduleDisposeVideo(IVideo video, MyListDeleteType? deleteType = null, MyListFetchMode fetchMode = MyListFetchMode.Auto, bool prioritize = false)
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
    private Task DisposeEntry(MyListDeleteType? deleteType, Func<MyListState, Task> mark, Func<Task> remove)
    {
        deleteType ??= settingsProvider.GetSettings().AniDb.MyList_DeleteType;
        if (deleteType is MyListDeleteType.DeleteLocalOnly)
            return Task.CompletedTask;

        return GetMarkedState(deleteType.Value) is { } state ? mark(state) : remove();
    }

    /// <summary>
    /// The storage state an entry is marked with for a given delete type, or
    /// <c>null</c> when the entry is to be removed outright rather than marked.
    /// </summary>
    private static MyListState? GetMarkedState(MyListDeleteType deleteType)
        => deleteType switch
        {
            MyListDeleteType.MarkDeleted => MyListState.Deleted,
            MyListDeleteType.MarkExternalStorage => MyListState.Remote,
            MyListDeleteType.MarkUnknown => MyListState.Unknown,
            MyListDeleteType.MarkDisk => MyListState.Disk,
            _ => null,
        };

    #endregion

    #endregion

    #region Sync

    public async Task<MyListSyncResult?> SyncAsync(MyListSyncOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (!await _syncLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogInformation("Skipping the MyList sync; one is already running");
            return null;
        }

        try
        {
            return await SyncInternalAsync(options, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task<MyListSyncResult> SyncInternalAsync(MyListSyncOptions? options, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Syncing the MyList");

        var fetchMode = ResolveFetchMode(options?.FetchMode ?? MyListFetchMode.Auto);
        var ignoreTimeCheck = fetchMode.HasFlag(MyListFetchMode.IgnoreTimeCheck);
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

        var entries = ignoreTimeCheck
            ? await FetchMyListAsync(cancellationToken)
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

        var missingFiles = await AddMissingFiles(localFiles, onlineFiles);

        var aniDBUser = users.GetAniDBUser();
        var modifiedSeries = new LinkedHashSet<AnimeSeries>();

        // Remove Missing Files and update watched states (single loop)
        var filesToRemove = new List<MyListEntry>();

        foreach (var myItem in onlineFiles.SelectMany(a => a))
        {
            try
            {
                totalItems++;
                if (myItem.ViewedAt.HasValue) watchedItems++;

                // Tier 1 (file level): entries for real files are matched to local files by their FileID
                // Tier 2 (episode level): generic entries are matched to episodes
                // Any entry that cannot be matched is treated as missing
                //
                // the entry carries the answer when the index resolved one; otherwise
                // all we have is the _generic_ file state, a convention plenty of
                // generic entries do not follow
                var isGeneric = myItem.IsGeneric ?? myItem.FileState is not (MyListFileState.Normal or MyListFileState.Corrupted);
                if (isGeneric)
                {
                    if (myItem.EpisodeID is not 0 && animeEpisodes.GetByAniDBEpisodeID(myItem.EpisodeID) is { } episode)
                    {
                        modifiedItems = await ProcessGenericEntry(aniDBUser, episode, myItem, modifiedItems, modifiedSeries, storageState, readWatched, readUnwatched, setWatched, setUnwatched, watchedSyncMode, updateStates);
                        continue;
                    }
                }
                else
                {
                    var aniFile = storedReleaseInfos.GetByReleaseURI($"{AnidbReleaseProvider.ReleasePrefix}{myItem.FileID}");

                    var vl = aniFile?.ED2K is null ? null : videoLocals.GetByEd2k(aniFile.ED2K);

                    if (vl != null)
                    {
                        // We have it, so process watched states and update storage states if needed
                        modifiedItems = await ProcessStates(aniDBUser, vl, myItem, modifiedItems, modifiedSeries, storageState, readWatched, readUnwatched, setWatched, setUnwatched, watchedSyncMode, updateStates);
                        continue;
                    }
                }

                if (deleteType is MyListDeleteType.DeleteLocalOnly)
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
                // entries from the HTTP export always carry a list ID — the cheapest
                // identification mode for either operation
                if (entry.MyListID is not 0)
                    await ScheduleDisposeEntry(entry.MyListID, deleteType);
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

        await Task.WhenAll(modifiedSeries.Select(a => seriesService.QueueUpdateStats(a)));

        if (unclassifiedItems > 0)
            _logger.LogWarning(
                "MYLIST Unclassified: left {Count} unmatched entries alone because we could not tell whether they are generic. Enable {Setting} to resolve them",
                unclassifiedItems, nameof(AniDbSettings.MyList_UseGenericFileIndex));

        _logger.LogInformation(
            "Process MyList: {TotalItems} Items, {MissingFiles} Added, {Count} Deleted, {WatchedItems} Watched, {ModifiedItems} Modified, {UnclassifiedItems} Unclassified",
            totalItems, missingFiles, filesToRemove.Count, watchedItems, modifiedItems, unclassifiedItems);

        return new MyListSyncResult
        {
            TotalEntries = totalItems,
            WatchedEntries = watchedItems,
            ModifiedEntries = modifiedItems,
            FilesQueuedForAdd = missingFiles,
            EntriesQueuedForRemoval = filesToRemove.Count,
            UnclassifiedEntries = unclassifiedItems,
        };
    }

    public Task ScheduleSync(MyListSyncOptions? options = null, bool prioritize = false)
    {
        // resolve up front, so the job carries a complete set by the time it
        // executes. Its options are non-nullable by design, so anything enqueued
        // without them would silently run on the job's defaults instead of the
        // user's settings
        var resolved = ResolveSyncOptions(options);
        return scheduler.Enqueue<SyncAniDBMyListJob>(a => a.Options = resolved, prioritize);
    }

    /// <summary>
    /// Fills every null field of <paramref name="options"/> in from the
    /// configured settings, so the result is fully resolved.
    /// </summary>
    private MyListSyncOptions ResolveSyncOptions(MyListSyncOptions? options)
    {
        var settings = settingsProvider.GetSettings();
        return new MyListSyncOptions
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
        };
    }

    #region Sync | Private

    private async Task<int> ProcessStates(
        JMMUser? aniDBUser,
        VideoLocal video,
        MyListEntry myItem,
        int modifiedItems,
        ISet<AnimeSeries> modifiedSeries,
        MyListState localState,
        bool readWatched,
        bool readUnwatched,
        bool setWatched,
        bool setUnwatched,
        MyListWatchedSyncMode watchedSyncMode,
        bool updateStates
    )
    {
        // check watched states, read the states if needed, and update differences
        // aggregate and assume if one AniDB User has watched it, it should be marked
        // if multiple have, then take the latest
        // compare the states and update if needed
        var localWatchedDate = aniDBUser is null ? null : videoLocalUsers.GetByUserAndVideoLocalID(aniDBUser.JMMUserID, video.VideoLocalID)?.WatchedDate;
        localWatchedDate = AniDBExtensions.TruncateToAniDBPrecision(localWatchedDate);

        var shouldUpdate = false;
        var updateDate = myItem.ViewedAt;

        // we don't support multiple AniDB accounts, so we can just only iterate to set states.
        // same-day updates (the entry was updated on the same day as the local watch) are
        // resolved by the watched sync mode, while older differences follow the read/set settings
        var sameDay = localWatchedDate is not null && myItem.UpdatedAt == DateOnly.FromDateTime(localWatchedDate.Value);
        if (sameDay)
        {
            switch (watchedSyncMode)
            {
                case MyListWatchedSyncMode.Ignore:
                    break;

                case MyListWatchedSyncMode.TrustLocal:
                    if (localWatchedDate is not null && !localWatchedDate.Equals(updateDate))
                    {
                        shouldUpdate = true;
                        updateDate = localWatchedDate.Value.ToUniversalTime();
                    }
                    else if (localWatchedDate is null && updateDate is not null)
                    {
                        shouldUpdate = true;
                        updateDate = null;
                    }

                    break;

                case MyListWatchedSyncMode.TrustRemote:
                    if (aniDBUser is not null)
                    {
                        if (localWatchedDate is null && updateDate is not null)
                        {
                            modifiedItems++;
                            await userDataService.ImportVideoUserData(video, aniDBUser, new()
                            {
                                ProgressPosition = TimeSpan.Zero,
                                LastPlayedAt = updateDate,
                                LastUpdatedAt = myItem.UpdatedAt.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                            }, "AniDB", false).ConfigureAwait(false);
                            video.AnimeEpisodes
                                .DistinctBy(a => a.AnimeSeriesID)
                                .Select(a => a.AnimeSeries)
                                .WhereNotNull()
                                .ForEach(a => modifiedSeries.Add(a));
                        }
                        else if (localWatchedDate is not null && updateDate is null)
                        {
                            modifiedItems++;
                            await userDataService.ImportVideoUserData(video, aniDBUser, new()
                            {
                                ProgressPosition = TimeSpan.Zero,
                                LastPlayedAt = null,
                                LastUpdatedAt = myItem.UpdatedAt.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                            }, "AniDB", false).ConfigureAwait(false);
                            video.AnimeEpisodes
                                .DistinctBy(a => a.AnimeSeriesID)
                                .Select(a => a.AnimeSeries)
                                .WhereNotNull()
                                .ForEach(a => modifiedSeries.Add(a));
                        }
                    }

                    break;
            }
        }
        else if (readWatched && localWatchedDate == null && updateDate != null)
        {
            if (aniDBUser is not null)
            {
                modifiedItems++;
                await userDataService.ImportVideoUserData(video, aniDBUser, new()
                {
                    ProgressPosition = TimeSpan.Zero,
                    LastPlayedAt = updateDate,
                    LastUpdatedAt = myItem.UpdatedAt.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                }, "AniDB", false).ConfigureAwait(false);
                video.AnimeEpisodes
                    .DistinctBy(a => a.AnimeSeriesID)
                    .Select(a => a.AnimeSeries)
                    .WhereNotNull()
                    .ForEach(a => modifiedSeries.Add(a));
            }
        }
        // if we did the previous, then we don't want to undo it
        else if (readUnwatched && localWatchedDate != null && updateDate == null)
        {
            if (aniDBUser is not null)
            {
                modifiedItems++;
                await userDataService.ImportVideoUserData(video, aniDBUser, new()
                {
                    ProgressPosition = TimeSpan.Zero,
                    LastPlayedAt = null,
                    LastUpdatedAt = myItem.UpdatedAt.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                }, "AniDB", false).ConfigureAwait(false);
                video.AnimeEpisodes
                    .DistinctBy(a => a.AnimeSeriesID)
                    .Select(a => a.AnimeSeries)
                    .WhereNotNull()
                    .ForEach(a => modifiedSeries.Add(a));
            }
        }
        else if (setUnwatched && localWatchedDate == null && updateDate != null)
        {
            shouldUpdate = true;
            updateDate = null;
        }
        else if (setWatched && localWatchedDate != null && !localWatchedDate.Equals(updateDate))
        {
            shouldUpdate = true;
            updateDate = localWatchedDate.Value.ToUniversalTime();
        }

        // check if the state needs to be updated
        if (updateStates && (int)myItem.State != (int)localState) shouldUpdate = true;

        if (!shouldUpdate)
            return modifiedItems;

        await scheduler.Enqueue<UpdateAniDBMyListEntryJob>(a =>
        {
            a.VideoID = video.VideoLocalID;
            a.Data = new MyListUpdateData { State = updateStates ? localState : null, IsViewed = updateDate != null, ViewedAt = updateDate };
            a.UpdateSeriesStats = false;
        });

        return modifiedItems;
    }

    private async Task<int> ProcessGenericEntry(
        JMMUser? aniDBUser,
        AnimeEpisode episode,
        MyListEntry myItem,
        int modifiedItems,
        ISet<AnimeSeries> modifiedSeries,
        MyListState localState,
        bool readWatched,
        bool readUnwatched,
        bool setWatched,
        bool setUnwatched,
        MyListWatchedSyncMode watchedSyncMode,
        bool updateStates
    )
    {
        // check watched states, read the states if needed, and update differences.
        // the same reconciliation as files, but at the episode level
        if (episode.AniDB_Episode is not { } aniDBEpisode)
            return modifiedItems;

        var localWatchedDate = aniDBUser is null ? null : episode.GetUserRecord(aniDBUser.JMMUserID)?.WatchedDate;
        localWatchedDate = AniDBExtensions.TruncateToAniDBPrecision(localWatchedDate);

        var shouldUpdate = false;
        var updateDate = myItem.ViewedAt;

        // we don't support multiple AniDB accounts, so we can just only iterate to set states.
        // same-day updates (the entry was updated on the same day as the local watch) are
        // resolved by the watched sync mode, while older differences follow the read/set settings
        var sameDay = localWatchedDate is not null && myItem.UpdatedAt == DateOnly.FromDateTime(localWatchedDate.Value);
        if (sameDay)
        {
            switch (watchedSyncMode)
            {
                case MyListWatchedSyncMode.Ignore:
                    break;

                case MyListWatchedSyncMode.TrustLocal:
                    if (localWatchedDate is not null && !localWatchedDate.Equals(updateDate))
                    {
                        shouldUpdate = true;
                        updateDate = localWatchedDate.Value.ToUniversalTime();
                    }
                    else if (localWatchedDate is null && updateDate is not null)
                    {
                        shouldUpdate = true;
                        updateDate = null;
                    }

                    break;

                case MyListWatchedSyncMode.TrustRemote:
                    if (aniDBUser is not null)
                    {
                        if (localWatchedDate is null && updateDate is not null)
                        {
                            modifiedItems++;
                            await userDataService.ImportEpisodeUserData(episode, aniDBUser, new()
                            {
                                LastPlayedAt = updateDate,
                                LastUpdatedAt = myItem.UpdatedAt.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                            }, "AniDB", VideoUserDataSaveReason.None, false).ConfigureAwait(false);
                            if (episode.AnimeSeries is { } series) modifiedSeries.Add(series);
                        }
                        else if (localWatchedDate is not null && updateDate is null)
                        {
                            modifiedItems++;
                            await userDataService.ImportEpisodeUserData(episode, aniDBUser, new()
                            {
                                LastPlayedAt = null,
                                LastUpdatedAt = myItem.UpdatedAt.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                            }, "AniDB", VideoUserDataSaveReason.None, false).ConfigureAwait(false);
                            if (episode.AnimeSeries is { } series) modifiedSeries.Add(series);
                        }
                    }

                    break;
            }
        }
        else if (readWatched && localWatchedDate == null && updateDate != null)
        {
            if (aniDBUser is not null)
            {
                modifiedItems++;
                await userDataService.ImportEpisodeUserData(episode, aniDBUser, new()
                {
                    LastPlayedAt = updateDate,
                    LastUpdatedAt = myItem.UpdatedAt.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                }, "AniDB", VideoUserDataSaveReason.None, false).ConfigureAwait(false);
                if (episode.AnimeSeries is { } series) modifiedSeries.Add(series);
            }
        }
        // if we did the previous, then we don't want to undo it
        else if (readUnwatched && localWatchedDate != null && updateDate == null)
        {
            if (aniDBUser is not null)
            {
                modifiedItems++;
                await userDataService.ImportEpisodeUserData(episode, aniDBUser, new()
                {
                    LastPlayedAt = null,
                    LastUpdatedAt = myItem.UpdatedAt.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                }, "AniDB", VideoUserDataSaveReason.None, false).ConfigureAwait(false);
                if (episode.AnimeSeries is { } series) modifiedSeries.Add(series);
            }
        }
        else if (setUnwatched && localWatchedDate == null && updateDate != null)
        {
            shouldUpdate = true;
            updateDate = null;
        }
        else if (setWatched && localWatchedDate != null && !localWatchedDate.Equals(updateDate))
        {
            shouldUpdate = true;
            updateDate = localWatchedDate.Value.ToUniversalTime();
        }

        // check if the state needs to be updated
        if (updateStates && (int)myItem.State != (int)localState) shouldUpdate = true;

        if (!shouldUpdate)
            return modifiedItems;

        await scheduler.Enqueue<UpdateAniDBMyListEntryJob>(a =>
        {
            a.AnimeID = aniDBEpisode.AnimeID;
            a.EpisodeType = aniDBEpisode.EpisodeType;
            a.EpisodeNumber = aniDBEpisode.EpisodeNumber;
            a.Data = new MyListUpdateData { State = updateStates ? localState : null, IsViewed = updateDate != null, ViewedAt = updateDate };
            a.UpdateSeriesStats = false;
        });

        return modifiedItems;
    }

    private async Task<int> AddMissingFiles(
        ILookup<string, StoredReleaseInfo> localFiles,
        ILookup<int, MyListEntry> onlineFiles
    )
    {
        if (!settingsProvider.GetSettings().AniDb.MyList_AddFiles)
            return 0;
        var missingFiles = 0;
        foreach (var vid in videoLocals.GetAll().Where(a => !string.IsNullOrEmpty(a.Hash)))
        {
            if (!TryGetFileID(localFiles, vid.Hash, out var fileID)) continue;
            // the file is in the local collection but not recorded online
            if (onlineFiles.Contains(fileID)) continue;
            missingFiles++;

            await scheduler.Enqueue<AddAniDBMyListEntryJob>(a =>
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
