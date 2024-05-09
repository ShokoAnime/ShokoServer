using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Scheduling.Jobs.Trakt;
using Shoko.Server.Scheduling.Jobs.TvDB;
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
        _logger.LogInformation("Processing CommandRequest_GetAnimeHTTP: {AnimeID}", AnimeID);
        if (ForceRefresh && _handler.IsBanned)
        {
            _logger.LogDebug("We're HTTP banned and requested a forced online update for anime with ID {AnimeID}", AnimeID);
            throw new AniDBBannedException
            {
                BanType = UpdateType.HTTPBan,
                BanExpires = _handler.BanTime?.AddHours(_handler.BanTimerResetLength)
            };
        }

        var scheduler = await _schedulerFactory.GetScheduler();
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
            response = await GetResponse(anime);
            if (response == null) return null;
        }
        // Else, try to load a cached xml file.
        else
        {
            var (success, xml) = await TryGetXmlFromCache();
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
                    });
                }
                throw;
            }
        }

        // Create or update the anime record,
        anime ??= new SVR_AniDB_Anime();
        var isNew = anime.AniDB_AnimeID == 0;
        var updated = await _animeCreator.CreateAnime(response, anime, 0);

        // then conditionally create the series record if it doesn't exist,
        var series = RepoFactory.AnimeSeries.GetByAnimeID(AnimeID);
        if (series == null && CreateSeriesEntry)
        {
            series = await CreateAnimeSeriesAndGroup(anime);
        }

        // and then create or update the episode records if we have an
        // existing series record.
        if (series != null)
        {
            await series.CreateAnimeEpisodes(anime);
            RepoFactory.AnimeSeries.Save(series, true, false);
        }

        SVR_AniDB_Anime.UpdateStatsByAnimeID(AnimeID);

        // Request an image download
        var imagesJob = _jobFactory.CreateJob<GetAniDBImagesJob>(job => job.AnimeID = AnimeID);
        await imagesJob.Process();

        // Emit anidb anime updated event.
        if (updated.Any())
        {
            ShokoEventHandler.Instance.OnSeriesUpdated(anime, isNew ? UpdateReason.Added : UpdateReason.Updated);

            // Re-schedule the videos to move/rename as required if something changed.
            var videos = updated.SelectMany(RepoFactory.CrossRef_File_Episode.GetByEpisodeID).Where(a => a != null)
                .Select(a => RepoFactory.VideoLocal.GetByHash(a.Hash)).Where(a => a != null).DistinctBy(a => a.VideoLocalID).ToList();

            foreach (var video in videos)
                await scheduler.StartJob<RenameMoveFileJob>(job => job.VideoLocalID = video.VideoLocalID);
        }

        await ProcessRelations(response);

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
            var (success, xml) = await TryGetXmlFromCache();
            if (!success) throw;

            try
            {
                response = _parser.Parse(AnimeID, xml);
            }
            catch
            {
                _logger.LogTrace("Failed to parse the cached AnimeDoc_{AnimeID}.xml file", AnimeID);
                // Queue the command to get the data when we're no longer banned if there is no anime record.
                await (await _schedulerFactory.GetScheduler()).StartJob<GetAniDBAnimeJob>(c =>
                {
                    c.AnimeID = AnimeID;
                    c.DownloadRelations = DownloadRelations;
                    c.RelDepth = RelDepth;
                    c.CacheOnly = false;
                    c.ForceRefresh = true;
                    c.CreateSeriesEntry = CreateSeriesEntry;
                });
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
        var xml = await _xmlUtils.LoadAnimeHTTPFromFile(AnimeID);
        if (xml != null) return (true, xml);

        if (!CacheOnly && _handler.IsBanned) _logger.LogTrace("We're HTTP Banned and unable to find a cached AnimeDoc_{AnimeID}.xml file", AnimeID);
        else _logger.LogTrace("Unable to find a cached AnimeDoc_{AnimeID}.xml file", AnimeID);

        if (!CacheOnly)
        {
            // Queue the command to get the data when we're no longer banned if there is no anime record.
            await (await _schedulerFactory.GetScheduler()).StartJob<GetAniDBAnimeJob>(c =>
            {
                c.AnimeID = AnimeID;
                c.DownloadRelations = DownloadRelations;
                c.RelDepth = RelDepth;
                c.CacheOnly = false;
                c.ForceRefresh = true;
                c.CreateSeriesEntry = CreateSeriesEntry;
            });
        }
        return (false, null);
    }

    public async Task<SVR_AnimeSeries> CreateAnimeSeriesAndGroup(SVR_AniDB_Anime anime)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        // Create a new AnimeSeries record
        var series = new SVR_AnimeSeries();

        series.Populate(anime);
        var grp = _animeGroupCreator.GetOrCreateSingleGroupForAnime(anime);
        series.AnimeGroupID = grp.AnimeGroupID;
        // Populate before making a group to ensure IDs and stats are set for group filters.
        RepoFactory.AnimeSeries.Save(series, false, false);

        // check for TvDB associations
        if (anime.Restricted == 0)
        {
            if (_settings.TvDB.AutoLink && !series.IsTvDBAutoMatchingDisabled) await scheduler.StartJob<SearchTvDBSeriesJob>(c => c.AnimeID = AnimeID);

            // check for Trakt associations
            if (_settings.TraktTv.Enabled && !string.IsNullOrEmpty(_settings.TraktTv.AuthToken) && !series.IsTraktAutoMatchingDisabled)
                await scheduler.StartJob<SearchTraktSeriesJob>(c => c.AnimeID = AnimeID);

            if (anime.AnimeType == (int)AnimeType.Movie && !series.IsTMDBAutoMatchingDisabled)
                await scheduler.StartJob<SearchTMDBSeriesJob>(c => c.AnimeID = AnimeID);
        }

        return series;
    }

    private async Task ProcessRelations(ResponseGetAnime response)
    {
        if (!DownloadRelations) return;
        if (_settings.AniDb.MaxRelationDepth <= 0) return;
        if (RelDepth > _settings.AniDb.MaxRelationDepth) return;
        if (!_settings.AutoGroupSeries && !_settings.AniDb.DownloadRelatedAnime) return;
        var scheduler = await _schedulerFactory.GetScheduler();

        // Queue or process the related series.
        foreach (var relation in response.Relations)
        {
            // Skip queuing/processing the command if the anime record were
            // recently updated.
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(relation.RelatedAnimeID);
            if (anime != null)
            {
                // Check when the anime was last updated online if we are
                // forcing a refresh and we're not banned, otherwise check when
                // the local anime record was last updated (be it from a fresh
                // online xml file or from a cached xml file).
                var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(relation.RelatedAnimeID);
                var updatedAt = ForceRefresh && !_handler.IsBanned && update != null ? update.UpdatedAt : anime.DateTimeUpdated;
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
            });
        }
    }

    public GetAniDBAnimeJob(IHttpConnectionHandler handler, HttpAnimeParser parser, AnimeCreator animeCreator, HttpXmlUtils xmlUtils,
        IRequestFactory requestFactory, ISchedulerFactory schedulerFactory, ISettingsProvider settingsProvider, AnimeGroupCreator animeGroupCreator, AniDBTitleHelper titleHelper, JobFactory jobFactory)
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
        _settings = settingsProvider.GetSettings();
    }

    protected GetAniDBAnimeJob() { }
}
