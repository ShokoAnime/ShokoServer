using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Exceptions;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Services;

public class AbstractAnidbService : IAniDBService
{
    private readonly ILogger<AbstractAnidbService> _logger;

    private readonly ISchedulerFactory _schedulerFactory;

    private readonly JobFactory _jobFactory;

    private readonly ISettingsProvider _settingsProvider;

    private readonly AniDB_AnimeRepository _anidbAnimeRepository;

    private readonly AnimeSeriesRepository _seriesRepository;

    private readonly AniDBTitleHelper _titleHelper;

    private readonly IUDPConnectionHandler _udpConnectionHandler;

    private readonly IHttpConnectionHandler _httpConnectionHandler;

    public AbstractAnidbService(
        ILogger<AbstractAnidbService> logger,
        ISchedulerFactory schedulerFactory,
        JobFactory jobFactory,
        ISettingsProvider settingsProvider,
        AniDB_AnimeRepository anidbAnimeRepository,
        AnimeSeriesRepository seriesRepository,
        AniDBTitleHelper titleHelper,
        IUDPConnectionHandler udpConnectionHandler,
        IHttpConnectionHandler httpConnectionHandler
    )
    {
        _logger = logger;
        _schedulerFactory = schedulerFactory;
        _jobFactory = jobFactory;
        _settingsProvider = settingsProvider;
        _anidbAnimeRepository = anidbAnimeRepository;
        _seriesRepository = seriesRepository;
        _titleHelper = titleHelper;
        _udpConnectionHandler = udpConnectionHandler;
        _httpConnectionHandler = httpConnectionHandler;

        ShokoEventHandler.Instance.AniDBBanned += OnAniDBBanned;
        ShokoEventHandler.Instance.AVDumpEvent += OnAVDumpEvent;
    }

    ~AbstractAnidbService()
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

    #region By Shoko Series

    /// <inheritdoc/>
    public Task<ISeries> RefreshForShokoSeries(IShokoSeries shokoSeries, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto)
    {
        var anidbAnime = shokoSeries.AnidbAnime;
        return RefreshInternal(anidbAnime.ID, anidbAnime, refreshMethod)!;
    }

    /// <inheritdoc/>
    public Task ScheduleRefreshForShokoSeries(IShokoSeries shokoSeries, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto, bool prioritize = false)
    {
        var anidbAnime = shokoSeries.AnidbAnime;
        return ScheduleRefreshInternal(anidbAnime.ID, refreshMethod, prioritize);
    }

    #endregion

    #region Internals

    private async Task<ISeries?> RefreshInternal(int anidbAnimeID, ISeries? anidbAnime = null, AnidbRefreshMethod refreshMethod = AnidbRefreshMethod.Auto)
    {
        if (refreshMethod is AnidbRefreshMethod.None)
            return anidbAnime;

        try
        {
            var settings = _settingsProvider.GetSettings();
            var job = _jobFactory.CreateJob<GetAniDBAnimeJob>();
            ConfigureJob(job, anidbAnimeID, settings, refreshMethod);
            var anime = await job.Process().ConfigureAwait(false);
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

        var settings = _settingsProvider.GetSettings();
        var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
        if (prioritize)
            await scheduler.StartJobNow<GetAniDBAnimeJob>(job => ConfigureJob(job, anidbAnimeID, settings, refreshMethod)).ConfigureAwait(false);
        else
            await scheduler.StartJob<GetAniDBAnimeJob>(job => ConfigureJob(job, anidbAnimeID, settings, refreshMethod)).ConfigureAwait(false);
    }

    private static void ConfigureJob(GetAniDBAnimeJob job, int anidbAnimeID, IServerSettings settings, AnidbRefreshMethod refreshMethod)
    {
        job.AnimeID = anidbAnimeID;

        // Use the defaults based on some settings.
        if (refreshMethod is AnidbRefreshMethod.Auto)
        {
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

    #endregion
}
