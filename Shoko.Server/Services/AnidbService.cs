using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Exceptions;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Scheduling.Jobs.Trakt;
using Shoko.Server.Settings;
using Shoko.Server.Tasks;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Services;

public class AnidbService : IAniDBService
{
    private readonly ILogger<AnidbService> _logger;

    private readonly IServiceProvider _serviceProvider;

    private readonly ISettingsProvider _settingsProvider;

    private readonly IRequestFactory _requestFactory;

    private readonly ISchedulerFactory _schedulerFactory;

    private readonly JobFactory _jobFactory;

    private readonly IUDPConnectionHandler _udpConnectionHandler;

    private readonly IHttpConnectionHandler _httpConnectionHandler;

    private readonly HttpXmlUtils _xmlUtils;

    private readonly HttpAnimeParser _httpParser;

    private readonly AniDBTitleHelper _titleHelper;

    // Lazy init. to prevent circular dependency.
    private AnimeCreator? _animeCreator;

    private readonly AnimeGroupCreator _animeGroupCreator;

    // Lazy init. to prevent circular dependency.
    private AnimeSeriesService? _seriesService;

    private readonly AniDB_AnimeRepository _anidbAnimeRepository;

    private readonly AniDB_AnimeUpdateRepository _anidbAnimeUpdateRepository;

    private readonly AnimeSeriesRepository _seriesRepository;

    private readonly CrossRef_File_EpisodeRepository _crossReferenceRepository;

    public AnidbService(
        ILogger<AnidbService> logger,
        IServiceProvider serviceProvider,
        ISettingsProvider settingsProvider,
        IRequestFactory requestFactory,
        ISchedulerFactory schedulerFactory,
        JobFactory jobFactory,
        IUDPConnectionHandler udpConnectionHandler,
        IHttpConnectionHandler httpConnectionHandler,
        HttpXmlUtils xmlUtils,
        HttpAnimeParser httpParser,
        AniDBTitleHelper titleHelper,
        AnimeGroupCreator animeGroupCreator,
        AniDB_AnimeRepository anidbAnimeRepository,
        AniDB_AnimeUpdateRepository anidbAnimeUpdateRepository,
        AnimeSeriesRepository seriesRepository,
        CrossRef_File_EpisodeRepository crossReferenceRepository
    )
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _settingsProvider = settingsProvider;
        _requestFactory = requestFactory;
        _schedulerFactory = schedulerFactory;
        _jobFactory = jobFactory;
        _udpConnectionHandler = udpConnectionHandler;
        _httpConnectionHandler = httpConnectionHandler;
        _xmlUtils = xmlUtils;
        _httpParser = httpParser;
        _titleHelper = titleHelper;
        _animeGroupCreator = animeGroupCreator;
        _anidbAnimeRepository = anidbAnimeRepository;
        _anidbAnimeUpdateRepository = anidbAnimeUpdateRepository;
        _seriesRepository = seriesRepository;
        _crossReferenceRepository = crossReferenceRepository;

        ShokoEventHandler.Instance.AniDBBanned += OnAniDBBanned;
        ShokoEventHandler.Instance.AVDumpEvent += OnAVDumpEvent;
    }

    ~AnidbService()
    {
        ShokoEventHandler.Instance.AniDBBanned -= OnAniDBBanned;
        ShokoEventHandler.Instance.AVDumpEvent -= OnAVDumpEvent;
    }

    #region Banned Status

    /// <inheritdoc/>
    public event EventHandler<AniDBBannedEventArgs>? AniDBBanned;

    /// <inheritdoc/>
    public bool IsAniDBUdpReachable => _udpConnectionHandler.IsAlive && _udpConnectionHandler.IsNetworkAvailable;

    /// <inheritdoc/>
    public bool IsAniDBUdpBanned => _udpConnectionHandler.IsBanned;

    /// <inheritdoc/>
    public bool IsAniDBHttpBanned => _httpConnectionHandler.IsBanned;

    public void OnAniDBBanned(object? sender, AniDBBannedEventArgs eventArgs)
    {
        AniDBBanned?.Invoke(this, eventArgs);
    }

    #endregion

    #region "Remote" Search

    /// <inheritdoc/>
    public IReadOnlyList<IAnidbAnimeSearchResult> Search(string query, bool fuzzy)
        => _titleHelper.SearchTitle(query, fuzzy)
            .Select(a => new AbstractAnidbAnimeSearchResult(a, _anidbAnimeRepository, _seriesRepository))
            .ToList();

    #endregion

    #region Refresh

    #region By AniDB Anime ID

    /// <inheritdoc/>
    public async Task<ISeries?> RefreshByID(int anidbAnimeID, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto)
    {
        if (anidbAnimeID <= 0)
            return null;

        var anime = _anidbAnimeRepository.GetByAnimeID(anidbAnimeID);
        return await RefreshInternal(anidbAnimeID, anime, refreshMethod).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ScheduleRefresh(ISeries anidbAnime, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, bool prioritize = false)
    {
        ArgumentNullException.ThrowIfNull(anidbAnime);
        if (anidbAnime is not SVR_AniDB_Anime)
            throw new ArgumentException("ISeries must be from AniDB");

        await ScheduleRefreshInternal(anidbAnime.ID, refreshMethod, prioritize).ConfigureAwait(false);
    }

    #endregion

    #region By AniDB Anime

    /// <inheritdoc/>
    public async Task<ISeries> Refresh(ISeries anidbAnime, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto)
    {
        ArgumentNullException.ThrowIfNull(anidbAnime);
        if (anidbAnime is not SVR_AniDB_Anime)
            throw new ArgumentException("ISeries must be from AniDB");

        return await RefreshInternal(anidbAnime.ID, anidbAnime, refreshMethod).ConfigureAwait(false) ?? anidbAnime;
    }

    /// <inheritdoc/>
    public Task ScheduleRefreshByID(int anidbAnimeID, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, bool prioritize = false)
        => ScheduleRefreshInternal(anidbAnimeID, refreshMethod, prioritize);

    #endregion

    #region Internals

    private async Task<ISeries?> RefreshInternal(int anidbAnimeID, ISeries? anidbAnime = null, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto)
    {
        if (refreshMethod is AnidbRefreshMethod.None)
            return anidbAnime;

        try
        {
            var job = CreateJobDetails(anidbAnimeID, refreshMethod);
            var anime = await Process(job).ConfigureAwait(false);
            return anime ?? anidbAnime;
        }
        catch (AniDBBannedException ex)
        {
            throw new AnidbHttpBannedException(ex) { BanExpires = ex.BanExpires };
        }
    }

    private async Task ScheduleRefreshInternal(int anidbAnimeID, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, bool prioritize = false)
    {
        if (refreshMethod is AnidbRefreshMethod.None)
            return;

        var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
        if (refreshMethod.HasFlag(AnidbRefreshMethod.Cache) && !refreshMethod.HasFlag(AnidbRefreshMethod.Remote))
        {
            if (prioritize)
                await scheduler.StartJobNow<GetLocalAniDBAnimeJob>(job => (job.AnimeID, job.RefreshMethod) = (anidbAnimeID, refreshMethod)).ConfigureAwait(false);
            else
                await scheduler.StartJob<GetLocalAniDBAnimeJob>(job => (job.AnimeID, job.RefreshMethod) = (anidbAnimeID, refreshMethod)).ConfigureAwait(false);
        }
        else
        {
            if (prioritize)
                await scheduler.StartJobNow<GetAniDBAnimeJob>(job => (job.AnimeID, job.RefreshMethod) = (anidbAnimeID, refreshMethod)).ConfigureAwait(false);
            else
                await scheduler.StartJob<GetAniDBAnimeJob>(job => (job.AnimeID, job.RefreshMethod) = (anidbAnimeID, refreshMethod)).ConfigureAwait(false);
        }
    }

    private AnidbJobDetails CreateJobDetails(int anidbAnimeID, AnidbRefreshMethod refreshMethod, int relationDepth = 0)
    {
        var job = new AnidbJobDetails { AnimeID = anidbAnimeID, RelDepth = relationDepth };

        // Use the defaults based on settings.
        if (refreshMethod is AnidbRefreshMethod.Auto)
        {
            var settings = _settingsProvider.GetSettings();
            job.DownloadRelations = settings.AutoGroupSeries || settings.AniDb.DownloadRelatedAnime;
            job.CreateSeriesEntry = settings.AniDb.AutomaticallyImportSeries;
        }
        // Toggle everything manually.
        else
        {
            job.UseCache = refreshMethod.HasFlag(AnidbRefreshMethod.Cache);
            job.UseRemote = refreshMethod.HasFlag(AnidbRefreshMethod.Remote);
            job.PreferCacheOverRemote = refreshMethod.HasFlag(AnidbRefreshMethod.PreferCacheOverRemote);
            job.DeferToRemoteIfUnsuccessful = refreshMethod.HasFlag(AnidbRefreshMethod.DeferToRemoteIfUnsuccessful);
            job.IgnoreTimeCheck = refreshMethod.HasFlag(AnidbRefreshMethod.IgnoreTimeCheck);
            job.IgnoreHttpBans = refreshMethod.HasFlag(AnidbRefreshMethod.IgnoreHttpBans);
            job.DownloadRelations = refreshMethod.HasFlag(AnidbRefreshMethod.DownloadRelations);
            job.CreateSeriesEntry = refreshMethod.HasFlag(AnidbRefreshMethod.CreateShokoSeries);
            job.SkipTmdbUpdate = refreshMethod.HasFlag(AnidbRefreshMethod.SkipTmdbUpdate);
        }

        return job;
    }

    public Task<SVR_AniDB_Anime?> Process(int anidbAnimeID, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Default, int relationDepth = 0)
        => Process(CreateJobDetails(anidbAnimeID, refreshMethod, relationDepth));

    private async Task<SVR_AniDB_Anime?> Process(AnidbJobDetails job)
    {
        _logger.LogInformation("Processing {Job}: {AnimeID}", nameof(GetAniDBAnimeJob), job.AnimeID);

        var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
        var anime = _anidbAnimeRepository.GetByAnimeID(job.AnimeID);
        var update = _anidbAnimeUpdateRepository.GetByAnimeID(job.AnimeID);
        var animeRecentlyUpdated = AnimeRecentlyUpdated(anime, update);

        Exception? ex = null;
        ResponseGetAnime? response = null;
        if (job.PreferCacheOverRemote && job.UseCache)
        {
            try
            {
                var (success, xml) = await TryGetXmlFromCache(job.AnimeID).ConfigureAwait(false);
                if (success)
                    response = _httpParser.Parse(job.AnimeID, xml);
            }
            catch (Exception e)
            {
                _logger.LogDebug("Failed to parse the cached AnimeDoc_{AnimeID}.xml file", job.AnimeID);
                ex = e;
            }
        }

        if (job.UseRemote && response is null)
        {
            try
            {
                if (_httpConnectionHandler.IsBanned && !job.IgnoreHttpBans)
                {
                    _logger.LogDebug("We're HTTP banned and requested a forced online update for anime with ID {AnimeID}", job.AnimeID);
                    throw new AniDBBannedException
                    {
                        BanType = UpdateType.HTTPBan,
                        BanExpires = _httpConnectionHandler.BanTime?.AddHours(_httpConnectionHandler.BanTimerResetLength)
                    };
                }

                if (!animeRecentlyUpdated || job.IgnoreTimeCheck)
                {
                    var request = _requestFactory.Create<RequestGetAnime>(r => (r.AnimeID, r.Force) = (job.AnimeID, job.IgnoreHttpBans));
                    var httpResponse = request.Send();
                    response = httpResponse.Response;

                    // If the response is null then we successfully got a response from the server
                    // but the ID does not belong to any anime.
                    if (response is null)
                    {
                        _logger.LogError("No such anime with ID: {AnimeID}", job.AnimeID);
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                if (e is AniDBBannedException)
                    _logger.LogTrace("We're HTTP banned and requested an online update for anime with ID {AnimeID}", job.AnimeID);
                else
                    _logger.LogError(e, "Failed to get an anime with ID: {AnimeID}", job.AnimeID);

                ex = e;
            }
        }

        if (!job.PreferCacheOverRemote && job.UseCache && response is null)
        {
            try
            {
                var (success, xml) = await TryGetXmlFromCache(job.AnimeID).ConfigureAwait(false);
                if (success)
                    response = _httpParser.Parse(job.AnimeID, xml);
            }
            catch (Exception e)
            {
                _logger.LogDebug("Failed to parse the cached AnimeDoc_{AnimeID}.xml file", job.AnimeID);
                ex ??= e;
            }
        }

        // If we failed to get the data from either source then throw the
        // exception (if one exists) or return null.
        if (response is null)
        {
            if (job.DeferToRemoteIfUnsuccessful)
            {
                // Queue the command to get the data when we're no longer banned if there is no anime record.
                await scheduler.StartJob<GetAniDBAnimeJob>(c =>
                {
                    c.AnimeID = job.AnimeID;
                    c.DownloadRelations = job.DownloadRelations;
                    c.RelDepth = job.RelDepth;
                    c.UseCache = false;
                    c.CreateSeriesEntry = job.CreateSeriesEntry;
                    c.SkipTmdbUpdate = job.SkipTmdbUpdate;
                }).ConfigureAwait(false);
            }
            if (ex is null)
                return null;
            throw ex;
        }

        // Create or update the anime record,
        anime ??= new SVR_AniDB_Anime();
        var isNew = anime.AniDB_AnimeID == 0;
        _animeCreator ??= _serviceProvider.GetRequiredService<AnimeCreator>();
        var (isUpdated, titlesUpdated, descriptionUpdated, shouldUpdateFiles, animeEpisodeChanges) = await _animeCreator.CreateAnime(response, anime, job.RelDepth).ConfigureAwait(false);

        // then conditionally create the series record if it doesn't exist,
        var series = _seriesRepository.GetByAnimeID(job.AnimeID);
        var seriesIsNew = series == null;
        var seriesUpdated = false;
        var seriesEpisodeChanges = new Dictionary<SVR_AnimeEpisode, UpdateReason>();
        var settings = _settingsProvider.GetSettings();
        if (series == null && job.CreateSeriesEntry)
            series = await CreateAnimeSeriesAndGroup(anime, job, settings);

        // and then create or update the episode records if we have an
        // existing series record.
        if (series != null)
        {
            _seriesService ??= _serviceProvider.GetRequiredService<AnimeSeriesService>();
            (seriesUpdated, seriesEpisodeChanges) = await _seriesService.CreateAnimeEpisodes(series).ConfigureAwait(false);
            _seriesRepository.Save(series, true, false);
        }

        await _jobFactory.CreateJob<RefreshAnimeStatsJob>(x => x.AnimeID = job.AnimeID).Process().ConfigureAwait(false);

        // Request an image download
        var imagesJob = _jobFactory.CreateJob<GetAniDBImagesJob>(c => (c.AnimeID, c.OnlyPosters) = (job.AnimeID, series == null));
        await imagesJob.Process().ConfigureAwait(false);

        // Emit anidb anime updated event.
        if (isNew || isUpdated || animeEpisodeChanges.Count > 0)
            ShokoEventHandler.Instance.OnSeriesUpdated(anime, isNew ? UpdateReason.Added : UpdateReason.Updated, animeEpisodeChanges);

        // Reset the cached preferred title if anime titles were updated.
        if (titlesUpdated)
            anime.ResetPreferredTitle();

        // Reset the cached titles if anime titles were updated or if series is new.
        if ((titlesUpdated || seriesIsNew) && series is not null)
        {
            series.ResetPreferredTitle();
            series.ResetAnimeTitles();
        }

        // Reset the cached description if anime description was updated or if series is new.
        if ((descriptionUpdated || seriesIsNew) && series is not null)
        {
            series.ResetPreferredOverview();
        }

        // Emit shoko series updated event.
        if (series is not null && (seriesIsNew || seriesUpdated || seriesEpisodeChanges.Count > 0))
            ShokoEventHandler.Instance.OnSeriesUpdated(series, seriesIsNew ? UpdateReason.Added : UpdateReason.Updated, seriesEpisodeChanges);

        // Re-schedule the videos to move/rename as required if something changed.
        if (settings.Plugins.Renamer.RelocateOnImport && (
            isNew || shouldUpdateFiles || animeEpisodeChanges.Count > 0 || seriesIsNew || seriesUpdated || seriesEpisodeChanges.Count > 0
        ))
        {
            var videos = new List<VideoLocal>();
            if (isNew || seriesIsNew || shouldUpdateFiles || seriesUpdated)
            {
                videos.AddRange(
                    _crossReferenceRepository.GetByAnimeID(job.AnimeID)
                        .WhereNotNull()
                        .Select(a => a.VideoLocal)
                        .WhereNotNull()
                        .DistinctBy(a => a.VideoLocalID)
                );
            }
            else
            {
                if (animeEpisodeChanges.Count > 0)
                    videos.AddRange(
                        animeEpisodeChanges.Keys
                            .SelectMany(a => _crossReferenceRepository.GetByEpisodeID(a.EpisodeID))
                            .WhereNotNull()
                            .Select(a => a.VideoLocal)
                            .WhereNotNull()
                            .DistinctBy(a => a.VideoLocalID)
                    );
                if (seriesEpisodeChanges.Count > 0)
                    videos.AddRange(
                        seriesEpisodeChanges.Keys
                            .SelectMany(a => _crossReferenceRepository.GetByEpisodeID(a.AniDB_EpisodeID))
                            .WhereNotNull()
                            .Select(a => a.VideoLocal)
                            .WhereNotNull()
                            .DistinctBy(a => a.VideoLocalID)
                    );
            }

            foreach (var video in videos)
                await scheduler.StartJob<RenameMoveFileJob>(job => job.VideoLocalID = video.VideoLocalID).ConfigureAwait(false);
        }

        if (!job.SkipTmdbUpdate)
            foreach (var xref in anime.TmdbShowCrossReferences)
                await scheduler.StartJob<UpdateTmdbShowJob>(job =>
                {
                    job.TmdbShowID = xref.TmdbShowID;
                    job.DownloadImages = true;
                }).ConfigureAwait(false);

        await ProcessRelations(response, job, settings).ConfigureAwait(false);

        return anime;
    }

    private bool AnimeRecentlyUpdated(SVR_AniDB_Anime? anime, AniDB_AnimeUpdate update)
    {
        if (anime != null && update != null)
        {
            var ts = DateTime.Now - update.UpdatedAt;
            var settings = _settingsProvider.GetSettings();
            if (ts.TotalHours < settings.AniDb.MinimumHoursToRedownloadAnimeInfo)
                return true;
        }

        return false;
    }

    private async Task<(bool success, string? xml)> TryGetXmlFromCache(int animeID)
    {
        var xml = await _xmlUtils.LoadAnimeHTTPFromFile(animeID).ConfigureAwait(false);
        if (xml != null)
            return (true, xml);

        if (_httpConnectionHandler.IsBanned)
            _logger.LogTrace("We're HTTP Banned and unable to find a cached AnimeDoc_{AnimeID}.xml file", animeID);
        else
            _logger.LogTrace("Unable to find a cached AnimeDoc_{AnimeID}.xml file", animeID);

        return (false, null);
    }

    private async Task<SVR_AnimeSeries> CreateAnimeSeriesAndGroup(SVR_AniDB_Anime anime, AnidbJobDetails job, IServerSettings settings)
    {
        // Create a new AnimeSeries record
        var series = new SVR_AnimeSeries
        {
            AniDB_ID = anime.AnimeID,
            LatestLocalEpisodeNumber = 0,
            DateTimeUpdated = DateTime.Now,
            DateTimeCreated = DateTime.Now,
            UpdatedAt = DateTime.Now,
            SeriesNameOverride = string.Empty
        };

        var grp = _animeGroupCreator.GetOrCreateSingleGroupForAnime(anime);
        series.AnimeGroupID = grp.AnimeGroupID;
        // Populate before making a group to ensure IDs and stats are set for group filters.
        _seriesRepository.Save(series, false, false);

        var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
        if (settings.TMDB.AutoLink && !series.IsTMDBAutoMatchingDisabled)
            await scheduler.StartJob<SearchTmdbJob>(c => c.AnimeID = job.AnimeID).ConfigureAwait(false);

        if (!anime.IsRestricted)
        {
            if (settings.TraktTv.Enabled && settings.TraktTv.AutoLink && !string.IsNullOrEmpty(settings.TraktTv.AuthToken) && !series.IsTraktAutoMatchingDisabled)
                await scheduler.StartJob<SearchTraktSeriesJob>(c => c.AnimeID = job.AnimeID).ConfigureAwait(false);
        }

        return series;
    }

    private async Task ProcessRelations(ResponseGetAnime response, AnidbJobDetails job, IServerSettings settings)
    {
        if (!job.DownloadRelations) return;
        if (settings.AniDb.MaxRelationDepth <= 0) return;
        if (job.RelDepth >= settings.AniDb.MaxRelationDepth) return;
        if (!settings.AutoGroupSeries && !settings.AniDb.DownloadRelatedAnime) return;
        var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);

        // Queue or process the related series.
        foreach (var relation in response.Relations)
        {
            // Skip queuing/processing the command if the anime record were
            // recently updated.
            var anime = _anidbAnimeRepository.GetByAnimeID(relation.RelatedAnimeID);
            if (anime != null)
            {
                // Check when the anime was last updated online if we are
                // forcing a refresh, and we're not banned, otherwise check when
                // the local anime record was last updated (be it from a fresh
                // online xml file or from a cached xml file).
                var update = _anidbAnimeUpdateRepository.GetByAnimeID(relation.RelatedAnimeID);
#pragma warning disable CS0618
                var updatedAt = job.UseRemote && (job.IgnoreHttpBans || !_httpConnectionHandler.IsBanned) && update != null ? update.UpdatedAt : anime.DateTimeUpdated;
#pragma warning restore CS0618
                var ts = DateTime.Now - updatedAt;
                if (ts.TotalHours < settings.AniDb.MinimumHoursToRedownloadAnimeInfo) continue;
            }

            // Append the command to the queue.
            if (job.UseCache && !job.UseRemote)
                await scheduler.StartJobNow<GetLocalAniDBAnimeJob>(c =>
                {
                    c.AnimeID = relation.RelatedAnimeID;
                    c.DownloadRelations = true;
                    c.RelDepth = job.RelDepth + 1;
                    c.IgnoreTimeCheck = job.IgnoreTimeCheck;
                    c.IgnoreHttpBans = job.IgnoreHttpBans;
                    c.CreateSeriesEntry = job.CreateSeriesEntry && settings.AniDb.AutomaticallyImportSeries;
                    c.SkipTmdbUpdate = job.SkipTmdbUpdate;
                }).ConfigureAwait(false);
            else
                await scheduler.StartJobNow<GetAniDBAnimeJob>(c =>
                {
                    c.AnimeID = relation.RelatedAnimeID;
                    c.DownloadRelations = true;
                    c.RelDepth = job.RelDepth + 1;
                    c.UseCache = job.UseCache;
                    c.UseRemote = job.UseRemote;
                    c.IgnoreTimeCheck = job.IgnoreTimeCheck;
                    c.IgnoreHttpBans = job.IgnoreHttpBans;
                    c.CreateSeriesEntry = job.CreateSeriesEntry && settings.AniDb.AutomaticallyImportSeries;
                    c.SkipTmdbUpdate = job.SkipTmdbUpdate;
                }).ConfigureAwait(false);
        }
    }
    #endregion

    #endregion

    #region AVDump

    /// <inheritdoc/>
    public event EventHandler<AVDumpEventArgs>? AVDumpEvent;

    /// <inheritdoc/>
    public bool IsAVDumpInstalled => AVDumpHelper.IsAVDumpInstalled;

    /// <inheritdoc/>
    public string? InstalledAVDumpVersion => AVDumpHelper.InstalledAVDumpVersion;

    /// <inheritdoc/>
    public string? AvailableAVDumpVersion => AVDumpHelper.AVDumpVersion;

    /// <inheritdoc/>
    public bool UpdateAvdump(bool force = false)
    {
        if (!force)
        {
            var expectedVersion = AVDumpHelper.AVDumpVersion;
            var installedVersion = AVDumpHelper.InstalledAVDumpVersion;
            if (string.Equals(expectedVersion, installedVersion))
                return false;
        }

        return AVDumpHelper.UpdateAVDump();
    }

    private void OnAVDumpEvent(object? sender, AVDumpEventArgs eventArgs)
    {
        AVDumpEvent?.Invoke(this, eventArgs);
    }

    /// <inheritdoc/>
    public async Task AvdumpVideos(params IVideo[] videos)
    {
        var videoSet = new HashSet<int>();
        var videoDictionary = new Dictionary<int, string>();
        foreach (var video in videos)
        {
            if (!videoSet.Add(video.ID))
                continue;

            if (video.Locations.FirstOrDefault(x => x.IsAvailable) is not { } location)
                continue;

            videoDictionary.Add(video.ID, location.Path);
        }

        await Task.Run(() => AVDumpHelper.DumpFiles(videoDictionary)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ScheduleAvdumpVideos(params IVideo[] videos)
    {
        var videoSet = new HashSet<int>();
        var videoDictionary = new Dictionary<int, string>();
        foreach (var video in videos)
        {
            if (!videoSet.Add(video.ID))
                continue;

            if (video.Locations.FirstOrDefault(x => x.IsAvailable) is not { } location)
                continue;

            videoDictionary.Add(video.ID, location.Path);
        }

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJobNow<AVDumpFilesJob>(a => a.Videos = videoDictionary);
    }

    /// <inheritdoc/>
    public async Task AvdumpVideoFiles(params IVideoFile[] videoFiles)
    {
        var videoSet = new HashSet<int>();
        var videoDictionary = new Dictionary<int, string>();
        foreach (var videoFile in videoFiles)
        {
            if (!videoFile.IsAvailable)
                continue;

            if (!videoSet.Add(videoFile.VideoID))
                continue;

            videoDictionary.Add(videoFile.ID, videoFile.Path);
        }

        await Task.Run(() => AVDumpHelper.DumpFiles(videoDictionary)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ScheduleAvdumpVideoFiles(params IVideoFile[] videoFiles)
    {
        var videoSet = new HashSet<int>();
        var videoDictionary = new Dictionary<int, string>();
        foreach (var videoFile in videoFiles)
        {
            if (!videoFile.IsAvailable)
                continue;

            if (!videoSet.Add(videoFile.VideoID))
                continue;

            videoDictionary.Add(videoFile.ID, videoFile.Path);
        }

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJobNow<AVDumpFilesJob>(a => a.Videos = videoDictionary);
    }

    #endregion

    #region Helper Classes

    private class AbstractAnidbAnimeSearchResult(SeriesSearch.SearchResult<ResponseAniDBTitles.Anime> searchResult, AniDB_AnimeRepository animeAnimeRepository, AnimeSeriesRepository seriesRepository) : IAnidbAnimeSearchResult
    {
        private readonly AniDB_AnimeRepository _anidbAnimeRepository = animeAnimeRepository;

        private readonly AnimeSeriesRepository _seriesRepository = seriesRepository;

        private IReadOnlyList<AnimeTitle>? _titles = null;

        private ISeries? _anidbAnime = null;

        private IShokoSeries? _shokoSeries = null;

        /// <inheritdoc/>
        public int ID { get; init; } = searchResult.Result.AnimeID;

        /// <inheritdoc/>
        public DataSourceEnum Source => DataSourceEnum.AniDB;

        /// <inheritdoc/>
        public string DefaultTitle { get; init; } = searchResult.Result.MainTitle;

        /// <inheritdoc/>
        public string PreferredTitle { get; init; } = searchResult.Result.PreferredTitle;

        /// <inheritdoc/>
        public IReadOnlyList<AnimeTitle> Titles => _titles ??= searchResult.Result.Titles
            .Select(a => new AnimeTitle()
            {
                Source = DataSourceEnum.AniDB,
                LanguageCode = a.LanguageCode,
                Language = a.Language,
                Title = a.Title,
                Type = a.TitleType,
            })
            .ToList();

        /// <inheritdoc/>
        public string MatchedTitle { get; init; } = searchResult.Match;

        /// <inheritdoc/>
        public bool ExactMatch { get; init; } = searchResult.ExactMatch;

        /// <inheritdoc/>
        public int Index { get; init; } = searchResult.Index;

        /// <inheritdoc/>
        public double Distance { get; init; } = searchResult.Distance;

        /// <inheritdoc/>
        public int LengthDifference { get; init; } = searchResult.LengthDifference;

        /// <inheritdoc/>
        public ISeries? AnidbAnime => ID > 0 ? _anidbAnime ??= _anidbAnimeRepository.GetByAnimeID(ID) : null;

        /// <inheritdoc/>
        public IShokoSeries? ShokoSeries => ID > 0 ? _shokoSeries ??= _seriesRepository.GetByAnimeID(ID) : null;
    }

    private class AnidbJobDetails
    {
        /// <summary>
        /// The ID of the AniDB anime to update.
        /// </summary>
        public int AnimeID { get; set; }

        /// <summary>
        /// Use the remote AniDB HTTP API.
        /// </summary>
        public bool UseRemote { get; set; } = true;

        /// <summary>
        /// Use the local AniDB HTTP cache.
        /// </summary>
        public bool UseCache { get; set; } = true;

        /// <summary>
        /// Prefer the local AniDB HTTP cache over the remote AniDB HTTP API.
        /// </summary>
        public bool PreferCacheOverRemote { get; set; }

        /// <summary>
        /// Defer to a later remote update if the current update fails.
        /// </summary>
        public bool DeferToRemoteIfUnsuccessful { get; set; } = true;

        /// <summary>
        /// Ignore the time check and forces a refresh even if the anime was
        /// recently updated.
        /// </summary>
        public bool IgnoreTimeCheck { get; set; }

        /// <summary>
        /// Ignore any active HTTP bans and forcefully asks the server for the data.
        /// </summary>
        public bool IgnoreHttpBans { get; set; }

        /// <summary>
        /// Download related anime until the maximum depth is reached.
        /// </summary>
        public bool DownloadRelations { get; set; }

        /// <summary>
        /// Create a Shoko series entry if one does not exist.
        /// </summary>
        public bool CreateSeriesEntry { get; set; }

        /// <summary>
        /// Skip updating related TMDB entities after update.
        /// </summary>
        public bool SkipTmdbUpdate { get; set; }

        /// <summary>
        /// Current depth of recursion.
        /// </summary>
        public int RelDepth { get; set; }
    }

    #endregion
}