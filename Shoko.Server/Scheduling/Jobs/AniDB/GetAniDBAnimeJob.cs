using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Scheduling.Jobs.Trakt;
using Shoko.Server.Scheduling.Jobs.TvDB;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Tasks;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBHttpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_HTTP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetAniDBAnimeJob : BaseJob<SVR_AniDB_Anime>
{
    private readonly IHttpConnectionHandler _handler;
    private readonly HttpAnimeParser _parser;
    private readonly AniDBTitleHelper _titleHelper;
    private readonly AnimeCreator _animeCreator;
    private readonly AnimeGroupCreator _animeGroupCreator;
    private readonly HttpXmlUtils _xmlUtils;
    private readonly JobFactory _jobFactory;
    private readonly IRequestFactory _requestFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IServerSettings _settings;
    private readonly AnimeSeriesService _seriesService;
    private string _animeName;

    public int AnimeID { get; set; }
    public bool ForceRefresh { get; set; }
    public bool CacheOnly { get; set; }
    public bool DownloadRelations { get; set; }
    public int RelDepth { get; set; }
    public bool CreateSeriesEntry { get; set; }

    public override void PostInit()
    {
        // We have the title helper. May as well use it to provide better info for the user
        _animeName = RepoFactory.AniDB_Anime?.GetByAnimeID(AnimeID)?.PreferredTitle ?? _titleHelper.SearchAnimeID(AnimeID)?.PreferredTitle;
    }

    public override string TypeName => "Get AniDB Anime Data";

    public override string Title => "Getting AniDB Anime Data";
    public override Dictionary<string, object> Details => _animeName == null ? new()
    {
        {
            "AnimeID", AnimeID
        }
    } : new() {
        {
            "Anime", _animeName
        }
    };

    public override async Task<SVR_AniDB_Anime> Process()
    {
        if (AnimeID == 0) return null;
        _logger.LogInformation("Processing {Job}: {AnimeID}", nameof(GetAniDBAnimeJob), AnimeID);
        if (ForceRefresh && _handler.IsBanned)
        {
            _logger.LogDebug("We're HTTP banned and requested a forced online update for anime with ID {AnimeID}", AnimeID);
            throw new AniDBBannedException
            {
                BanType = UpdateType.HTTPBan,
                BanExpires = _handler.BanTime?.AddHours(_handler.BanTimerResetLength)
            };
        }

        var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
        var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(AnimeID);
        var animeRecentlyUpdated = AnimeRecentlyUpdated(anime, update);

        // If we're not only using the cache, the anime was not recently
        // updated, we're not http banned, and the user requested a forced
        // online refresh _or_ if there is no local anime record, then try
        // to fetch a new updated record online but fallback to loading from
        // the cache unless we request a forced online refresh.
        ResponseGetAnime response;
        if (!CacheOnly && !animeRecentlyUpdated && !_handler.IsBanned && (ForceRefresh || anime == null))
        {
            response = await GetResponse(anime).ConfigureAwait(false);
            if (response == null) return null;
        }
        // Else, try to load a cached xml file.
        else
        {
            var (success, xml) = await TryGetXmlFromCache().ConfigureAwait(false);
            if (!success) return null;

            try
            {
                response = _parser.Parse(AnimeID, xml);
            }
            catch
            {
                _logger.LogDebug("Failed to parse the cached AnimeDoc_{AnimeID}.xml file", AnimeID);
                if (!CacheOnly)
                {
                    // Queue the command to get the data when we're no longer banned if there is no anime record.
                    await scheduler.StartJob<GetAniDBAnimeJob>(c =>
                    {
                        c.AnimeID = AnimeID;
                        c.DownloadRelations = DownloadRelations;
                        c.RelDepth = RelDepth;
                        c.CacheOnly = false;
                        c.ForceRefresh = true;
                        c.CreateSeriesEntry = CreateSeriesEntry;
                    }).ConfigureAwait(false);
                }
                throw;
            }
        }

        // Create or update the anime record,
        anime ??= new SVR_AniDB_Anime();
        var isNew = anime.AniDB_AnimeID == 0;
        var (isUpdated, titlesUpdated, descriptionUpdated, animeEpisodeChanges) = await _animeCreator.CreateAnime(response, anime, RelDepth).ConfigureAwait(false);

        // then conditionally create the series record if it doesn't exist,
        var series = RepoFactory.AnimeSeries.GetByAnimeID(AnimeID);
        var seriesIsNew = series == null;
        var seriesUpdated = false;
        var seriesEpisodeChanges = new Dictionary<SVR_AnimeEpisode, UpdateReason>();
        if (series == null && CreateSeriesEntry)
        {
            series = await CreateAnimeSeriesAndGroup(anime);
        }

        // and then create or update the episode records if we have an
        // existing series record.
        if (series != null)
        {
            (seriesUpdated, seriesEpisodeChanges) = await _seriesService.CreateAnimeEpisodes(series).ConfigureAwait(false);
            RepoFactory.AnimeSeries.Save(series, true, false);
        }

        await _jobFactory.CreateJob<RefreshAnimeStatsJob>(x => x.AnimeID = AnimeID).Process().ConfigureAwait(false);

        // Request an image download
        var imagesJob = _jobFactory.CreateJob<GetAniDBImagesJob>(job =>
        {
            job.AnimeID = AnimeID;
            job.OnlyPosters = series == null;
        });
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
        if (isNew || isUpdated || animeEpisodeChanges.Count > 0 || seriesIsNew || seriesUpdated || seriesEpisodeChanges.Count > 0)
        {
            var videos = new List<SVR_VideoLocal>();
            if (isNew || seriesIsNew || isUpdated || seriesUpdated)
            {
                videos.AddRange(
                    RepoFactory.CrossRef_File_Episode.GetByAnimeID(AnimeID)
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
                            .SelectMany(a => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(a.EpisodeID))
                            .WhereNotNull()
                            .Select(a => a.VideoLocal)
                            .WhereNotNull()
                            .DistinctBy(a => a.VideoLocalID)
                    );
                if (seriesEpisodeChanges.Count > 0)
                    videos.AddRange(
                        seriesEpisodeChanges.Keys
                            .SelectMany(a => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(a.AniDB_EpisodeID))
                            .WhereNotNull()
                            .Select(a => a.VideoLocal)
                            .WhereNotNull()
                            .DistinctBy(a => a.VideoLocalID)
                    );
            }

            foreach (var video in videos)
                await scheduler.StartJob<RenameMoveFileJob>(job => job.VideoLocalID = video.VideoLocalID).ConfigureAwait(false);

            if (isNew || animeEpisodeChanges.Count > 0)
                foreach (var xref in anime.TmdbShowCrossReferences)
                    await scheduler.StartJob<UpdateTmdbShowJob>(job => job.TmdbShowID = xref.TmdbShowID).ConfigureAwait(false);
        }

        await ProcessRelations(response).ConfigureAwait(false);

        return anime;
    }

    private async Task<ResponseGetAnime> GetResponse(SVR_AniDB_Anime anime)
    {
        ResponseGetAnime response;
        try
        {
            var request = _requestFactory.Create<RequestGetAnime>(r => r.AnimeID = AnimeID);
            var httpResponse = request.Send();
            response = httpResponse.Response;
            if (response == null)
            {
                _logger.LogError("No such anime with ID: {AnimeID}", AnimeID);
                return null;
            }
        }
        catch (AniDBBannedException)
        {
            // Don't even try to load from the cache if we requested a
            // forced online refresh.
            if (anime != null)
            {
                _logger.LogTrace("We're HTTP banned and requested a forced online update for anime with ID {AnimeID}", AnimeID);
                throw;
            }

            // If the anime record doesn't exist yet then try to load it
            // from the cache. A stall record is better than no record
            // in most cases.
            var (success, xml) = await TryGetXmlFromCache().ConfigureAwait(false);
            if (!success) throw;

            try
            {
                response = _parser.Parse(AnimeID, xml);
            }
            catch
            {
                _logger.LogTrace("Failed to parse the cached AnimeDoc_{AnimeID}.xml file", AnimeID);
                // Queue the command to get the data when we're no longer banned if there is no anime record.
                var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
                await scheduler.StartJob<GetAniDBAnimeJob>(c =>
                {
                    c.AnimeID = AnimeID;
                    c.DownloadRelations = DownloadRelations;
                    c.RelDepth = RelDepth;
                    c.CacheOnly = false;
                    c.ForceRefresh = true;
                    c.CreateSeriesEntry = CreateSeriesEntry;
                }).ConfigureAwait(false);
                throw;
            }

            _logger.LogTrace("We're HTTP banned but were able to load the cached AnimeDoc_{AnimeID}.xml file from the cache", AnimeID);
        }

        return response;
    }

    private bool AnimeRecentlyUpdated(SVR_AniDB_Anime anime, AniDB_AnimeUpdate update)
    {
        var animeRecentlyUpdated = false;
        if (anime != null && update != null)
        {
            var ts = DateTime.Now - update.UpdatedAt;
            if (ts.TotalHours < _settings.AniDb.MinimumHoursToRedownloadAnimeInfo)
            {
                animeRecentlyUpdated = true;
            }
        }

        return animeRecentlyUpdated;
    }

    private async Task<(bool success, string xml)> TryGetXmlFromCache()
    {
        var xml = await _xmlUtils.LoadAnimeHTTPFromFile(AnimeID).ConfigureAwait(false);
        if (xml != null) return (true, xml);

        if (!CacheOnly && _handler.IsBanned) _logger.LogTrace("We're HTTP Banned and unable to find a cached AnimeDoc_{AnimeID}.xml file", AnimeID);
        else _logger.LogTrace("Unable to find a cached AnimeDoc_{AnimeID}.xml file", AnimeID);

        if (!CacheOnly)
        {
            // Queue the command to get the data when we're no longer banned if there is no anime record.
            var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
            await scheduler.StartJob<GetAniDBAnimeJob>(c =>
            {
                c.AnimeID = AnimeID;
                c.DownloadRelations = DownloadRelations;
                c.RelDepth = RelDepth;
                c.CacheOnly = false;
                c.ForceRefresh = true;
                c.CreateSeriesEntry = CreateSeriesEntry;
            }).ConfigureAwait(false);
        }
        return (false, null);
    }

    public async Task<SVR_AnimeSeries> CreateAnimeSeriesAndGroup(SVR_AniDB_Anime anime)
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
        RepoFactory.AnimeSeries.Save(series, false, false);

        var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
        if (_settings.TMDB.AutoLink && !series.IsTMDBAutoMatchingDisabled)
            await scheduler.StartJob<SearchTmdbJob>(c => c.AnimeID = AnimeID).ConfigureAwait(false);

        if (anime.Restricted == 0)
        {
            if (_settings.TvDB.AutoLink && !series.IsTvDBAutoMatchingDisabled)
                await scheduler.StartJob<SearchTvDBSeriesJob>(c => c.AnimeID = AnimeID).ConfigureAwait(false);

            if (_settings.TraktTv.Enabled && _settings.TraktTv.AutoLink && !string.IsNullOrEmpty(_settings.TraktTv.AuthToken) && !series.IsTraktAutoMatchingDisabled)
                await scheduler.StartJob<SearchTraktSeriesJob>(c => c.AnimeID = AnimeID).ConfigureAwait(false);
        }

        return series;
    }

    private async Task ProcessRelations(ResponseGetAnime response)
    {
        if (!DownloadRelations) return;
        if (_settings.AniDb.MaxRelationDepth <= 0) return;
        if (RelDepth > _settings.AniDb.MaxRelationDepth) return;
        if (!_settings.AutoGroupSeries && !_settings.AniDb.DownloadRelatedAnime) return;
        var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);

        // Queue or process the related series.
        foreach (var relation in response.Relations)
        {
            // Skip queuing/processing the command if the anime record were
            // recently updated.
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(relation.RelatedAnimeID);
            if (anime != null)
            {
                // Check when the anime was last updated online if we are
                // forcing a refresh, and we're not banned, otherwise check when
                // the local anime record was last updated (be it from a fresh
                // online xml file or from a cached xml file).
                var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(relation.RelatedAnimeID);
#pragma warning disable CS0618
                var updatedAt = ForceRefresh && !_handler.IsBanned && update != null ? update.UpdatedAt : anime.DateTimeUpdated;
#pragma warning restore CS0618
                var ts = DateTime.Now - updatedAt;
                if (ts.TotalHours < _settings.AniDb.MinimumHoursToRedownloadAnimeInfo) continue;
            }

            // Append the command to the queue.
            await scheduler.StartJobNow<GetAniDBAnimeJob>(c =>
            {
                c.AnimeID = relation.RelatedAnimeID;
                c.DownloadRelations = true;
                c.RelDepth = RelDepth + 1;
                c.CacheOnly = !ForceRefresh && CacheOnly;
                c.ForceRefresh = ForceRefresh;
                c.CreateSeriesEntry = CreateSeriesEntry && _settings.AniDb.AutomaticallyImportSeries;
            }).ConfigureAwait(false);
        }
    }

    public GetAniDBAnimeJob(IHttpConnectionHandler handler, HttpAnimeParser parser, AnimeCreator animeCreator, HttpXmlUtils xmlUtils,
        IRequestFactory requestFactory, ISchedulerFactory schedulerFactory, ISettingsProvider settingsProvider, AnimeGroupCreator animeGroupCreator, AniDBTitleHelper titleHelper, JobFactory jobFactory, AnimeSeriesService seriesService)
    {
        _handler = handler;
        _parser = parser;
        _animeCreator = animeCreator;
        _xmlUtils = xmlUtils;
        _requestFactory = requestFactory;
        _schedulerFactory = schedulerFactory;
        _animeGroupCreator = animeGroupCreator;
        _titleHelper = titleHelper;
        _jobFactory = jobFactory;
        _seriesService = seriesService;
        _settings = settingsProvider.GetSettings();
    }

    protected GetAniDBAnimeJob() { }
}
