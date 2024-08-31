using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Commons.Utils;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Metro;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Filters;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Constants = Shoko.Server.Server.Constants;

namespace Shoko.Server;

[ApiController]
[Route("/api/Metro")]
[ApiVersion("1.0", Deprecated = true)]
public class ShokoServiceImplementationMetro : IShokoServerMetro, IHttpContextAccessor
{
    private readonly TraktTVHelper _traktHelper;

    private readonly ShokoServiceImplementation _service;

    private readonly ISettingsProvider _settingsProvider;

    private readonly JobFactory _jobFactory;

    private readonly WatchedStatusService _watchedService;

    private readonly AnimeEpisodeService _epService;

    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public HttpContext HttpContext { get; set; }

    public ShokoServiceImplementationMetro(TraktTVHelper traktHelper, ISettingsProvider settingsProvider, ShokoServiceImplementation service,
        JobFactory jobFactory, WatchedStatusService watchedService, AnimeEpisodeService epService)
    {
        _traktHelper = traktHelper;
        _settingsProvider = settingsProvider;
        _service = service;
        _jobFactory = jobFactory;
        _watchedService = watchedService;
        _epService = epService;
    }

    [HttpGet("Server/Status")]
    public CL_ServerStatus GetServerStatus()
    {
        var contract = new CL_ServerStatus();

        try
        {
            var httpHandler = HttpContext.RequestServices.GetRequiredService<IHttpConnectionHandler>();
            var udpHandler = HttpContext.RequestServices.GetRequiredService<IUDPConnectionHandler>();
            contract.HashQueueCount = 0;
            contract.HashQueueMessage = string.Empty;
            contract.HashQueueState = string.Empty;
            contract.HashQueueStateId = 0;
            contract.HashQueueStateParams = [];
            contract.GeneralQueueCount = 0;
            contract.GeneralQueueMessage = string.Empty;
            contract.GeneralQueueState = string.Empty;
            contract.GeneralQueueStateId = 0;
            contract.GeneralQueueStateParams = [];
            contract.ImagesQueueCount = 0;
            contract.ImagesQueueMessage = string.Empty;
            contract.ImagesQueueState = string.Empty;
            contract.ImagesQueueStateId = 0;
            contract.ImagesQueueStateParams = [];

            contract.IsBanned = httpHandler.IsBanned || udpHandler.IsBanned;
            contract.BanReason = (httpHandler.IsBanned ? httpHandler.BanTime : udpHandler.BanTime).ToString();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
        }

        return contract;
    }

    [HttpPost("Server/Settings")]
    public CL_ServerSettings GetServerSettings()
    {
        var contract = new CL_ServerSettings();

        try
        {
            return _settingsProvider.GetSettings().ToContract();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
        }

        return contract;
    }

    [HttpPost("Comment/{traktID}/{commentText}/{isSpoiler}")]
    public CL_Response<bool> PostCommentShow(string traktID, string commentText, bool isSpoiler)
    {
        return _traktHelper.PostCommentShow(traktID, commentText, isSpoiler);
    }

    [HttpGet("Community/Links/{animeID}")]
    public Metro_CommunityLinks GetCommunityLinks(int animeID)
    {
        var contract = new Metro_CommunityLinks();
        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            if (anime is null)
            {
                return null;
            }

            //AniDB
            contract.AniDB_ID = animeID;
            contract.AniDB_URL = string.Format(Constants.URLS.AniDB_Series, animeID);
            contract.AniDB_DiscussURL = string.Format(Constants.URLS.AniDB_SeriesDiscussion, animeID);

            // MAL
            var malRef = anime.GetCrossRefMAL();
            if (malRef is not null && malRef.Count > 0)
            {
                contract.MAL_ID = malRef[0].MALID.ToString();
                contract.MAL_URL = string.Format(Constants.URLS.MAL_Series, malRef[0].MALID);
                contract.MAL_DiscussURL = string.Format(Constants.URLS.MAL_Series, malRef[0].MALID);
            }

            // TvDB
            var tvdbRef = anime.TvdbSeriesCrossReferences;
            if (tvdbRef is not null && tvdbRef.Count > 0)
            {
                contract.TvDB_ID = tvdbRef[0].TvDBID.ToString();
                contract.TvDB_URL = string.Format(Constants.URLS.TvDB_Series, tvdbRef[0].TvDBID);
            }

            // Trakt
            var traktRef = anime.GetCrossRefTraktV2();
            if (traktRef is not null && traktRef.Count > 0)
            {
                contract.Trakt_ID = traktRef[0].TraktID;
                contract.Trakt_URL = string.Format(Constants.URLS.Trakt_Series, traktRef[0].TraktID);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
        }

        return contract;
    }

    [HttpPost("User/Auth/{username}/{password}")]
    public JMMUser AuthenticateUser(string username, string password)
    {
        try
        {
            return RepoFactory.JMMUser.AuthenticateUser(username, password);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
            return null;
        }
    }

    [HttpGet("User")]
    public List<JMMUser> GetAllUsers()
    {
        // get all the users
        try
        {
            return RepoFactory.JMMUser.GetAll().Cast<JMMUser>().ToList();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
        }

        return [];
    }

    [HttpGet("Group/{userID}")]
    public List<CL_AnimeGroup_User> GetAllGroups(int userID)
    {
        try
        {
            var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
            return RepoFactory.AnimeGroup.GetAll()
                .Select(a => groupService.GetV1Contract(a, userID))
                .OrderBy(a => a.SortName).ToList();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
        }

        return [];
    }

    [NonAction]
    public List<CL_AnimeEpisode_User> GetEpisodesRecentlyAddedSummary(int maxRecords, int userID)
    {
        var retEps = new List<CL_AnimeEpisode_User>();
        try
        {
            {
                var user = RepoFactory.JMMUser.GetByID(userID);
                if (user is null)
                {
                    return retEps;
                }

                /*string sql = "Select ae.AnimeSeriesID, max(vl.DateTimeCreated) as MaxDate " +
                             "From VideoLocal vl " +
                             "INNER JOIN CrossRef_File_Episode xref ON vl.Hash = xref.Hash " +
                             "INNER JOIN AnimeEpisode ae ON ae.AniDB_EpisodeID = xref.EpisodeID " +
                             "GROUP BY ae.AnimeSeriesID " +
                             "ORDER BY MaxDate desc ";
                */

                var results = RepoFactory.VideoLocal.GetMostRecentlyAdded(maxRecords, userID)
                    .SelectMany(a => a.AnimeEpisodes).GroupBy(a => a.AnimeSeriesID)
                    .Select(a => (a.Key, a.Max(b => b.DateTimeUpdated)));

                var numEps = 0;
                foreach ((var animeSeriesID, var lastUpdated) in results)
                {
                    var ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                    if (ser is null)
                    {
                        continue;
                    }

                    if (!user.AllowedSeries(ser))
                    {
                        continue;
                    }


                    var vids =
                        RepoFactory.VideoLocal.GetMostRecentlyAddedForAnime(1, ser.AniDB_ID);
                    if (vids.Count == 0)
                    {
                        continue;
                    }

                    var eps = vids[0].AnimeEpisodes;
                    if (eps.Count == 0)
                    {
                        continue;
                    }

                    var epContract = _epService.GetV1Contract(eps[0], userID);
                    if (epContract is not null)
                    {
                        retEps.Add(epContract);
                        numEps++;

                        // Lets only return the specified amount
                        if (retEps.Count == maxRecords)
                        {
                            return retEps;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
        }

        return retEps;
    }

    [HttpGet("Anime/New/{maxRecords}/{userID}")]
    public List<Metro_Anime_Summary> GetAnimeWithNewEpisodes(int maxRecords, int userID)
    {
        var retAnime = new List<Metro_Anime_Summary>();
        try
        {
            {
                var user = RepoFactory.JMMUser.GetByID(userID);
                if (user is null)
                {
                    return retAnime;
                }

                var results = RepoFactory.VideoLocal.GetMostRecentlyAdded(maxRecords, userID)
                    .SelectMany(a => a.AnimeEpisodes).GroupBy(a => a.AnimeSeriesID)
                    .Select(a => (a.Key, a.Max(b => b.DateTimeUpdated)));

                var numEps = 0;
                foreach ((var animeSeriesID, var lastUpdated) in results)
                {
                    var ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                    if (ser is null)
                    {
                        continue;
                    }

                    if (!user.AllowedSeries(ser))
                    {
                        continue;
                    }

                    var serUser = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, ser.AnimeSeriesID);

                    var vids =
                        RepoFactory.VideoLocal.GetMostRecentlyAddedForAnime(1, ser.AniDB_ID);
                    if (vids.Count == 0)
                    {
                        continue;
                    }

                    var eps = vids[0].AnimeEpisodes;
                    if (eps.Count == 0)
                    {
                        continue;
                    }

                    var epContract = _epService.GetV1Contract(eps[0], userID);
                    if (epContract is not null)
                    {
                        var anidb_anime = ser.AniDB_Anime;

                        var summary = new Metro_Anime_Summary
                        {
                            AnimeID = ser.AniDB_ID,
                            AnimeName = ser.PreferredTitle,
                            AnimeSeriesID = ser.AnimeSeriesID,
                            BeginYear = anidb_anime.BeginYear,
                            EndYear = anidb_anime.EndYear
                        };
                        if (serUser is not null)
                        {
                            summary.UnwatchedEpisodeCount = serUser.UnwatchedEpisodeCount;
                        }
                        else
                        {
                            summary.UnwatchedEpisodeCount = 0;
                        }

                        var imgDet = anidb_anime.PreferredOrDefaultPoster;
                        summary.PosterName = imgDet.LocalPath;
                        summary.ImageType = (int)imgDet.ImageType.ToClient(imgDet.Source);
                        summary.ImageID = imgDet.ID;

                        retAnime.Add(summary);
                        numEps++;

                        // Lets only return the specified amount
                        if (retAnime.Count == maxRecords)
                        {
                            return retAnime;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
        }

        return retAnime;
    }

    [NonAction]
    public List<Metro_Anime_Summary> GetAnimeContinueWatching_old(int maxRecords, int userID)
    {
        var retAnime = new List<Metro_Anime_Summary>();
        try
        {
            {
                var start = DateTime.Now;

                var user = RepoFactory.JMMUser.GetByID(userID);
                if (user is null)
                {
                    return retAnime;
                }

                // get a list of series that is applicable
                var allSeriesUser =
                    RepoFactory.AnimeSeries_User.GetMostRecentlyWatched(userID);

                var ts = DateTime.Now - start;
                _logger.Info(string.Format("GetAnimeContinueWatching:Series: {0}", ts.TotalMilliseconds));

                foreach (var userRecord in allSeriesUser)
                {
                    start = DateTime.Now;

                    var series = RepoFactory.AnimeSeries.GetByID(userRecord.AnimeSeriesID);
                    if (series is null)
                    {
                        continue;
                    }

                    if (!user.AllowedSeries(series))
                    {
                        _logger.Info(string.Format("GetAnimeContinueWatching:Skipping Anime - not allowed: {0}",
                            series.AniDB_ID));
                        continue;
                    }

                    var serUser = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, series.AnimeSeriesID);

                    var ep = _service.GetNextUnwatchedEpisode(userRecord.AnimeSeriesID,
                        userID);
                    if (ep is not null)
                    {
                        var anidb_anime = series.AniDB_Anime;

                        var summary = new Metro_Anime_Summary
                        {
                            AnimeID = series.AniDB_ID,
                            AnimeName = series.PreferredTitle,
                            AnimeSeriesID = series.AnimeSeriesID,
                            BeginYear = anidb_anime.BeginYear,
                            EndYear = anidb_anime.EndYear
                        };

                        if (serUser is not null)
                        {
                            summary.UnwatchedEpisodeCount = serUser.UnwatchedEpisodeCount;
                        }
                        else
                        {
                            summary.UnwatchedEpisodeCount = 0;
                        }

                        var imgDet = anidb_anime.PreferredOrDefaultPoster;
                        summary.PosterName = imgDet.LocalPath;
                        summary.ImageType = (int)imgDet.ImageType.ToClient(imgDet.Source);
                        summary.ImageID = imgDet.ID;

                        retAnime.Add(summary);

                        ts = DateTime.Now - start;
                        _logger.Info(string.Format("GetAnimeContinueWatching:Anime: {0} - {1}", summary.AnimeName, ts.TotalMilliseconds));

                        // Lets only return the specified amount
                        if (retAnime.Count == maxRecords)
                        {
                            return retAnime;
                        }
                    }
                    else
                    {
                        _logger.Info(string.Format("GetAnimeContinueWatching:Skipping Anime - no episodes: {0}",
                            series.AniDB_ID));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
        }

        return retAnime;
    }

    [HttpGet("Anime/ContinueWatch/{maxRecords}/{userID}")]
    public List<Metro_Anime_Summary> GetAnimeContinueWatching(int maxRecords, int userID)
    {
        var retAnime = new List<Metro_Anime_Summary>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
            {
                return retAnime;
            }

            // find the locked Continue Watching Filter
            FilterPreset gf = null;
            var lockedGFs = RepoFactory.FilterPreset.GetLockedGroupFilters();
            if (lockedGFs is not null)
            {
                // if it already exists we can leave
                foreach (var gfTemp in lockedGFs.Where(gfTemp => gfTemp.Name == "Continue Watching"))
                {
                    gf = gfTemp;
                    break;
                }
            }

            if (gf is null) return retAnime;

            var evaluator = HttpContext.RequestServices.GetRequiredService<FilterEvaluator>();
            var results = evaluator.EvaluateFilter(gf, user.JMMUserID);

            var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
            var comboGroups = results.Select(a => RepoFactory.AnimeGroup.GetByID(a.Key)).Where(a => a is not null)
                .Select(a => groupService.GetV1Contract(a, userID));

            foreach (var grp in comboGroups)
            {
                foreach (var ser in RepoFactory.AnimeSeries.GetByGroupID(grp.AnimeGroupID))
                {
                    if (!user.AllowedSeries(ser)) continue;

                    var serUser = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, ser.AnimeSeriesID);

                    var ep = _service.GetNextUnwatchedEpisode(ser.AnimeSeriesID, userID);
                    if (ep is not null)
                    {
                        var anidb_anime = ser.AniDB_Anime;

                        var summary = new Metro_Anime_Summary
                        {
                            AnimeID = ser.AniDB_ID,
                            AnimeName = ser.PreferredTitle,
                            AnimeSeriesID = ser.AnimeSeriesID,
                            BeginYear = anidb_anime.BeginYear,
                            EndYear = anidb_anime.EndYear,
                            PosterName = anidb_anime.PreferredOrDefaultPosterPath,
                        };

                        if (serUser is not null)
                        {
                            summary.UnwatchedEpisodeCount = serUser.UnwatchedEpisodeCount;
                        }
                        else
                        {
                            summary.UnwatchedEpisodeCount = 0;
                        }

                        var imgDet = anidb_anime.PreferredOrDefaultPoster;
                        summary.ImageType = (int)imgDet.ImageType.ToClient(imgDet.Source);
                        summary.ImageID = imgDet.ID;

                        retAnime.Add(summary);

                        // Lets only return the specified amount
                        if (retAnime.Count == maxRecords)
                        {
                            return retAnime;
                        }
                    }
                    else
                    {
                        _logger.Info(string.Format("GetAnimeContinueWatching:Skipping Anime - no episodes: {0}",
                            ser.AniDB_ID));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
        }

        return retAnime;
    }

    [HttpGet("Anime/Calendar/{userID}/{startDateSecs}/{endDateSecs}/{maxRecords}")]
    public List<Metro_Anime_Summary> GetAnimeCalendar(int userID, int startDateSecs, int endDateSecs, int maxRecords)
    {
        var retAnime = new List<Metro_Anime_Summary>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
            {
                return retAnime;
            }

            var startDate = AniDB.GetAniDBDateAsDate(startDateSecs);
            var endDate = AniDB.GetAniDBDateAsDate(endDateSecs);

            var allAnime = RepoFactory.AniDB_Anime.GetForDate(startDate.Value, endDate.Value);
            foreach (var anidb_anime in allAnime)
            {
                if (!user.AllowedAnime(anidb_anime))
                {
                    continue;
                }

                var ser = RepoFactory.AnimeSeries.GetByAnimeID(anidb_anime.AnimeID);

                var summary = new Metro_Anime_Summary
                {
                    AirDateAsSeconds = anidb_anime.GetAirDateAsSeconds(),
                    AnimeID = anidb_anime.AnimeID
                };
                if (ser is not null)
                {
                    summary.AnimeName = ser.PreferredTitle;
                    summary.AnimeSeriesID = ser.AnimeSeriesID;
                }
                else
                {
                    summary.AnimeName = anidb_anime.MainTitle;
                    summary.AnimeSeriesID = 0;
                }

                summary.BeginYear = anidb_anime.BeginYear;
                summary.EndYear = anidb_anime.EndYear;

                var imgDet = anidb_anime.PreferredOrDefaultPoster;
                summary.PosterName = imgDet.LocalPath;
                summary.ImageType = (int)imgDet.ImageType.ToClient(imgDet.Source);
                summary.ImageID = imgDet.ID;

                retAnime.Add(summary);
                if (retAnime.Count == maxRecords)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
        }

        return retAnime;
    }

    [HttpGet("Anime/Search/{userID}/{queryText}/{maxRecords}")]
    public List<Metro_Anime_Summary> SearchAnime(int userID, string queryText, int maxRecords)
    {
        var retAnime = new List<Metro_Anime_Summary>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
            {
                return retAnime;
            }


            var allAnime = RepoFactory.AniDB_Anime.SearchByName(queryText);
            foreach (var anidb_anime in allAnime)
            {
                if (!user.AllowedAnime(anidb_anime))
                {
                    continue;
                }

                var ser = RepoFactory.AnimeSeries.GetByAnimeID(anidb_anime.AnimeID);

                var summary = new Metro_Anime_Summary
                {
                    AirDateAsSeconds = anidb_anime.GetAirDateAsSeconds(),
                    AnimeID = anidb_anime.AnimeID
                };
                if (ser is not null)
                {
                    summary.AnimeName = ser.PreferredTitle;
                    summary.AnimeSeriesID = ser.AnimeSeriesID;
                }
                else
                {
                    summary.AnimeName = anidb_anime.MainTitle;
                    summary.AnimeSeriesID = 0;
                }

                summary.BeginYear = anidb_anime.BeginYear;
                summary.EndYear = anidb_anime.EndYear;

                var imgDet = anidb_anime.PreferredOrDefaultPoster;
                summary.PosterName = imgDet.LocalPath;
                summary.ImageType = (int)imgDet.ImageType.ToClient(imgDet.Source);
                summary.ImageID = imgDet.ID;

                retAnime.Add(summary);
                if (retAnime.Count == maxRecords)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
        }

        return retAnime;
    }

    [HttpGet("Anime/Detail/{animeID}/{userID}/{maxEpisodeRecords}")]
    public Metro_Anime_Detail GetAnimeDetail(int animeID, int userID, int maxEpisodeRecords)
    {
        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            if (anime is null)
            {
                return null;
            }

            var ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

            var ret = new Metro_Anime_Detail { AnimeID = anime.AnimeID };
            if (ser is not null)
            {
                ret.AnimeName = ser.PreferredTitle;
            }
            else
            {
                ret.AnimeName = anime.MainTitle;
            }

            if (ser is not null)
            {
                ret.AnimeSeriesID = ser.AnimeSeriesID;
            }
            else
            {
                ret.AnimeSeriesID = 0;
            }

            ret.BeginYear = anime.BeginYear;
            ret.EndYear = anime.EndYear;

            var imgDet = anime.PreferredOrDefaultPoster;
            ret.PosterImageType = (int)imgDet.ImageType.ToClient(imgDet.Source);
            ret.PosterImageID = imgDet.ID;

            ret.FanartImageType = 0;
            ret.FanartImageID = 0;

            ret.AnimeType = anime.GetAnimeTypeDescription();
            ret.Description = anime.Description;
            ret.EpisodeCountNormal = anime.EpisodeCountNormal;
            ret.EpisodeCountSpecial = anime.EpisodeCountSpecial;


            ret.AirDate = anime.AirDate;
            ret.EndDate = anime.EndDate;

            ret.OverallRating = anime.GetAniDBRating();
            ret.TotalVotes = anime.GetAniDBTotalVotes();
            ret.AllTags = string.Join('|', anime.Tags.Select(tag => tag.TagName).Distinct());

            ret.NextEpisodesToWatch = [];
            if (ser is not null)
            {
                var serUserRec = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, ser.AnimeSeriesID);
                if (ser is not null)
                {
                    ret.UnwatchedEpisodeCount = serUserRec.UnwatchedEpisodeCount;
                }
                else
                {
                    ret.UnwatchedEpisodeCount = 0;
                }


                var animeEpisodeList = new List<SVR_AnimeEpisode>();
                var dictEpUsers = new Dictionary<int, SVR_AnimeEpisode_User>();
                foreach (
                    var userRecord in
                    RepoFactory.AnimeEpisode_User.GetByUserIDAndSeriesID(userID, ser.AnimeSeriesID))
                {
                    dictEpUsers[userRecord.AnimeEpisodeID] = userRecord;
                }

                foreach (var animeEpisode in RepoFactory.AnimeEpisode.GetBySeriesID(ser.AnimeSeriesID))
                {
                    if (!dictEpUsers.ContainsKey(animeEpisode.AnimeEpisodeID))
                    {
                        animeEpisodeList.Add(animeEpisode);
                        continue;
                    }

                    var usrRec = dictEpUsers[animeEpisode.AnimeEpisodeID];
                    if (usrRec.WatchedCount == 0 || !usrRec.WatchedDate.HasValue)
                    {
                        animeEpisodeList.Add(animeEpisode);
                    }
                }

                var anidbEpisodeList = RepoFactory.AniDB_Episode.GetByAnimeID(ser.AniDB_ID);
                var animeEpisodeDict = new Dictionary<int, SVR_AniDB_Episode>();
                foreach (var anidbEpisode in anidbEpisodeList)
                {
                    animeEpisodeDict[anidbEpisode.EpisodeID] = anidbEpisode;
                }

                var candidateEps = new List<CL_AnimeEpisode_User>();

                foreach (var ep in animeEpisodeList)
                {
                    if (animeEpisodeDict.TryGetValue(ep.AniDB_EpisodeID, out var anidbEpisode))
                    {
                        if (anidbEpisode.EpisodeTypeEnum is EpisodeType.Episode or EpisodeType.Special)
                        {
                            // The episode list have already been filtered to only episodes with a user record
                            // So just add the candidate to the list.
                            candidateEps.Add(_epService.GetV1Contract(ep, userID));
                        }
                    }
                }

                if (candidateEps.Count > 0)
                {

                    // sort by episode type and number to find the next episode

                    // this will generate a lot of queries when the user doesn't have files
                    // for these episodes
                    var cnt = 0;
                    foreach (var canEp in candidateEps.OrderBy(a => a.EpisodeType)
                                 .ThenBy(a => a.EpisodeNumber))
                    {
                        if (animeEpisodeDict.TryGetValue(canEp.AniDB_EpisodeID, out var anidbEpisode))
                        {
                            dictEpUsers.TryGetValue(canEp.AnimeEpisodeID, out var userEpRecord);

                            // now refresh from the database to get file count
                            var epFresh = RepoFactory.AnimeEpisode.GetByID(canEp.AnimeEpisodeID);

                            var fileCount = epFresh.VideoLocals.Count;
                            if (fileCount > 0)
                            {
                                var contract = new Metro_Anime_Episode
                                {
                                    AnimeEpisodeID = epFresh.AnimeEpisodeID,
                                    LocalFileCount = fileCount
                                };
                                if (userEpRecord is null)
                                {
                                    contract.IsWatched = false;
                                }
                                else
                                {
                                    contract.IsWatched = userEpRecord.WatchedCount > 0;
                                }

                                // anidb
                                contract.EpisodeNumber = anidbEpisode.EpisodeNumber;
                                contract.EpisodeName = epFresh.PreferredTitle;

                                contract.EpisodeType = anidbEpisode.EpisodeType;
                                contract.LengthSeconds = anidbEpisode.LengthSeconds;
                                contract.AirDate = anidbEpisode.GetAirDateFormatted();

                                // tvdb
                                SetTvDBInfo(anidbEpisode, ref contract);

                                ret.NextEpisodesToWatch.Add(contract);
                                cnt++;
                            }
                        }

                        if (cnt == maxEpisodeRecords)
                        {
                            break;
                        }
                    }
                }
            }

            return ret;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
            return null;
        }
    }

    [HttpGet("Anime/Summary/{animeID}")]
    public Metro_Anime_Summary GetAnimeSummary(int animeID)
    {
        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            if (anime is null)
            {
                return null;
            }

            var ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

            var summary = new Metro_Anime_Summary
            {
                AnimeID = anime.AnimeID,
                AnimeName = anime.PreferredTitle,
                AnimeSeriesID = 0,
                BeginYear = anime.BeginYear,
                EndYear = anime.EndYear,
            };
            var imgDet = anime.PreferredOrDefaultPoster;
            summary.PosterName = imgDet.LocalPath;
            summary.ImageType = (int)imgDet.ImageType.ToClient(imgDet.Source);
            summary.ImageID = imgDet.ID;

            if (ser is not null)
            {
                summary.AnimeName = ser.PreferredTitle;
                summary.AnimeSeriesID = ser.AnimeSeriesID;
            }

            return summary;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
        }

        return null;
    }


    [NonAction]
    public static void SetTvDBInfo(SVR_AniDB_Episode ep, ref Metro_Anime_Episode contract)
    {
        var override_link = RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAniDBEpisodeID(ep.EpisodeID);
        if (override_link.Any(a => a is not null))
        {
            var tvdbEpisode = RepoFactory.TvDB_Episode.GetByTvDBID(override_link.FirstOrDefault().TvDBEpisodeID);
            contract.EpisodeName = tvdbEpisode.EpisodeName;
            contract.EpisodeOverview = tvdbEpisode.Overview;
            contract.ImageID = tvdbEpisode.TvDB_EpisodeID;
            contract.ImageType = (int)CL_ImageEntityType.TvDB_Episode;
            return;
        }

        var link = RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAniDBEpisodeID(ep.EpisodeID);
        if (link.Any(a => a is not null))
        {
            var tvdbEpisode = RepoFactory.TvDB_Episode.GetByTvDBID(link.FirstOrDefault().TvDBEpisodeID);
            contract.EpisodeName = tvdbEpisode.EpisodeName;
            contract.EpisodeOverview = tvdbEpisode.Overview;
            contract.ImageID = tvdbEpisode.TvDB_EpisodeID;
            contract.ImageType = (int)CL_ImageEntityType.TvDB_Episode;
        }
    }

    [HttpGet("Anime/Character/{animeID}/{maxRecords}")]
    public List<Metro_AniDB_Character> GetCharactersForAnime(int animeID, int maxRecords)
    {
        var chars = new List<Metro_AniDB_Character>();

        try
        {
            var animeChars = RepoFactory.AniDB_Anime_Character.GetByAnimeID(animeID)
                .OrderByDescending(item => item.CharType.Equals("main character in", StringComparison.InvariantCultureIgnoreCase))
                .ToList();
            if (animeChars.Count == 0)
            {
                return chars;
            }

            var index = 0;

            // first get all the main characters
            foreach (var animeChar in animeChars)
            {
                index++;
                var character = RepoFactory.AniDB_Character.GetByID(animeChar.CharID);
                if (character is not null)
                {
                    var contract = new Metro_AniDB_Character();
                    chars.Add(character.ToContractMetro(animeChar));
                }

                if (index == maxRecords)
                {
                    break;
                }
            }

        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
        }

        return chars;
    }

    [HttpGet("Anime/Comment/{animeID}/{maxRecords}")]
    public List<Metro_Comment> GetTraktCommentsForAnime(int animeID, int maxRecords)
    {
        return [];
    }

    [HttpGet("Anime/Recommendation/{animeID}/{maxRecords}")]
    public List<Metro_Comment> GetAniDBRecommendationsForAnime(int animeID, int maxRecords)
    {
        return [];
    }

    [HttpGet("Anime/Similar/{animeID}/{maxRecords}/{userID}")]
    public List<Metro_Anime_Summary> GetSimilarAnimeForAnime(int animeID, int maxRecords, int userID)
    {
        var links = new List<CL_AniDB_Anime_Similar>();
        var retAnime = new List<Metro_Anime_Summary>();
        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            if (anime is null)
            {
                return retAnime;
            }

            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user is null)
            {
                return retAnime;
            }


            // first get the related anime
            foreach (AniDB_Anime_Relation link in anime.RelatedAnime)
            {
                var animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(link.RelatedAnimeID);

                if (animeLink is null)
                {
                    // try getting it from anidb now
                    var job = _jobFactory.CreateJob<GetAniDBAnimeJob>(c =>
                    {
                        c.DownloadRelations = false;
                        c.AnimeID = link.RelatedAnimeID;
                        c.CreateSeriesEntry = false;
                    });
                    animeLink = job.Process().Result;
                }

                if (animeLink is null)
                {
                    continue;
                }

                if (!user.AllowedAnime(animeLink))
                {
                    continue;
                }

                // check if this anime has a series
                var ser = RepoFactory.AnimeSeries.GetByAnimeID(link.RelatedAnimeID);

                var summary = new Metro_Anime_Summary
                {
                    AnimeID = animeLink.AnimeID,
                    AnimeName = animeLink.MainTitle,
                    AnimeSeriesID = 0,
                    BeginYear = animeLink.BeginYear,
                    EndYear = animeLink.EndYear,

                    RelationshipType = link.RelationType
                };
                var imgDet = animeLink.PreferredOrDefaultPoster;
                summary.PosterName = imgDet.LocalPath;
                summary.ImageType = (int)imgDet.ImageType.ToClient(imgDet.Source);
                summary.ImageID = imgDet.ID;

                if (ser is not null)
                {
                    summary.AnimeName = ser.PreferredTitle;
                    summary.AnimeSeriesID = ser.AnimeSeriesID;
                }

                retAnime.Add(summary);
            }

            // now get similar anime
            foreach (var link in anime.SimilarAnime)
            {
                var animeLink =
                    RepoFactory.AniDB_Anime.GetByAnimeID(link.SimilarAnimeID);

                if (animeLink is null)
                {
                    // try getting it from anidb now
                    var job = _jobFactory.CreateJob<GetAniDBAnimeJob>(
                        c =>
                        {
                            c.DownloadRelations = false;
                            c.AnimeID = link.SimilarAnimeID;
                            c.CreateSeriesEntry = false;
                        }
                    );

                    animeLink = job.Process().Result;
                }

                if (animeLink is null)
                {
                    continue;
                }

                if (!user.AllowedAnime(animeLink))
                {
                    continue;
                }

                // check if this anime has a series
                var ser = RepoFactory.AnimeSeries.GetByAnimeID(link.SimilarAnimeID);

                var summary = new Metro_Anime_Summary
                {
                    AnimeID = animeLink.AnimeID,
                    AnimeName = animeLink.MainTitle,
                    AnimeSeriesID = 0,
                    BeginYear = animeLink.BeginYear,
                    EndYear = animeLink.EndYear,
                    RelationshipType = "Recommendation"
                };
                var imgDet = animeLink.PreferredOrDefaultPoster;
                summary.PosterName = imgDet.LocalPath;
                summary.ImageType = (int)imgDet.ImageType.ToClient(imgDet.Source);
                summary.ImageID = imgDet.ID;

                if (ser is not null)
                {
                    summary.AnimeName = ser.PreferredTitle;
                    summary.AnimeSeriesID = ser.AnimeSeriesID;
                }

                retAnime.Add(summary);

                if (retAnime.Count == maxRecords)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
        }

        return retAnime;
    }

    [HttpGet("Episode/Files/{episodeID}/{userID}")]
    public List<CL_VideoDetailed> GetFilesForEpisode(int episodeID, int userID)
    {
        try
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(episodeID);
            return ep is not null
                ? _epService.GetV1VideoDetailedContracts(ep, userID)
                : [];
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
        }

        return [];
    }

    [HttpGet("Episode/Watch/{animeEpisodeID}/{watchedStatus}/{userID}")]
    public CL_Response<CL_AnimeEpisode_User> ToggleWatchedStatusOnEpisode(int animeEpisodeID,
        bool watchedStatus, int userID)
    {
        var response = new CL_Response<CL_AnimeEpisode_User> { ErrorMessage = string.Empty, Result = null };
        try
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(animeEpisodeID);
            if (ep is null)
            {
                response.ErrorMessage = "Could not find anime episode record";
                return response;
            }

            _watchedService.SetWatchedStatus(ep, watchedStatus, true, DateTime.Now, false, userID, true).GetAwaiter().GetResult();
            var seriesService = Utils.ServiceContainer.GetRequiredService<AnimeSeriesService>();
            var series = ep.AnimeSeries;
            seriesService.UpdateStats(series, true, false);
            var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
            groupService.UpdateStatsFromTopLevel(series?.AnimeGroup?.TopLevelAnimeGroup, true, true);

            // refresh from db
            ep = RepoFactory.AnimeEpisode.GetByID(animeEpisodeID);

            response.Result = _epService.GetV1Contract(ep, userID);

            return response;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
            response.ErrorMessage = ex.Message;
            return response;
        }
    }

    [HttpGet("Anime/Refresh/{animeID}")]
    public string UpdateAnimeData(int animeID)
    {
        try
        {
            var job = _jobFactory.CreateJob<GetAniDBAnimeJob>(c =>
            {
                c.ForceRefresh = true;
                c.DownloadRelations = false;
                c.AnimeID = animeID;
                c.CreateSeriesEntry = false;
            });
            job.Process().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.ToString());
        }

        return string.Empty;
    }
}
