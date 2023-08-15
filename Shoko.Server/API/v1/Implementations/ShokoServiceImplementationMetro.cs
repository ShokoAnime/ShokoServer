using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Commons.Utils;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Metro;
using Shoko.Models.Server;
using Shoko.Models.TvDB;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Constants = Shoko.Server.Server.Constants;

namespace Shoko.Server;

[ApiController]
[Route("/api/Metro")]
[ApiVersion("1.0", Deprecated = true)]
public class ShokoServiceImplementationMetro : IShokoServerMetro, IHttpContextAccessor
{
    private readonly ICommandRequestFactory _commandFactory;
    private readonly TraktTVHelper _traktHelper;
    private readonly ShokoServiceImplementation _service;
    private readonly ISettingsProvider _settingsProvider;
    public HttpContext HttpContext { get; set; }

    private static Logger logger = LogManager.GetCurrentClassLogger();

    public ShokoServiceImplementationMetro(ICommandRequestFactory commandFactory, TraktTVHelper traktHelper, ISchedulerFactory schedulerFactory, ISettingsProvider settingsProvider)
    {
        _commandFactory = commandFactory;
        _traktHelper = traktHelper;
        _settingsProvider = settingsProvider;
        _service = new ShokoServiceImplementation(null, traktHelper, null, commandFactory, schedulerFactory, settingsProvider);
    }

    [HttpGet("Server/Status")]
    public CL_ServerStatus GetServerStatus()
    {
        var contract = new CL_ServerStatus();

        try
        {
            var httpHandler = HttpContext.RequestServices.GetRequiredService<IHttpConnectionHandler>();
            var udpHandler = HttpContext.RequestServices.GetRequiredService<IUDPConnectionHandler>();
            var hasherQueueState = !ShokoService.CmdProcessorHasher.Paused ? (
                ShokoService.CmdProcessorHasher.QueueState
            ) : (
                new()
                {
                    queueState = Shoko.Models.Queue.QueueStateEnum.Paused,
                    message = "Paused",
                    extraParams = new string[0],
                }
            );
            contract.HashQueueCount = ShokoService.CmdProcessorHasher.QueueCount;
            contract.HashQueueMessage = hasherQueueState.formatMessage();
            contract.HashQueueState = hasherQueueState.formatMessage(); // Deprecated since 3.6.0.0
            contract.HashQueueStateId = (int)hasherQueueState.queueState;
            contract.HashQueueStateParams = hasherQueueState.extraParams;

            var generalQueueState = !ShokoService.CmdProcessorGeneral.Paused ? (
                ShokoService.CmdProcessorGeneral.QueueState
            ) : (
                new()
                {
                    queueState = Shoko.Models.Queue.QueueStateEnum.Paused,
                    message = "Paused",
                    extraParams = new string[0],
                }
            );
            contract.GeneralQueueCount = ShokoService.CmdProcessorGeneral.QueueCount;
            contract.GeneralQueueMessage = generalQueueState.formatMessage();
            contract.GeneralQueueState = generalQueueState.formatMessage(); // Deprecated since 3.6.0.0
            contract.GeneralQueueStateId = (int)generalQueueState.queueState;
            contract.GeneralQueueStateParams = generalQueueState.extraParams;

            var imagesQueueState = !ShokoService.CmdProcessorImages.Paused ? (
                ShokoService.CmdProcessorImages.QueueState
            ) : (
                new()
                {
                    queueState = Shoko.Models.Queue.QueueStateEnum.Paused,
                    message = "Paused",
                    extraParams = new string[0],
                }
            );
            contract.ImagesQueueCount = ShokoService.CmdProcessorImages.QueueCount;
            contract.ImagesQueueMessage = imagesQueueState.formatMessage();
            contract.ImagesQueueState = imagesQueueState.formatMessage(); // Deprecated since 3.6.0.0
            contract.ImagesQueueStateId = (int)imagesQueueState.queueState;
            contract.ImagesQueueStateParams = imagesQueueState.extraParams;

            contract.IsBanned = httpHandler.IsBanned || udpHandler.IsBanned;
            contract.BanReason = (httpHandler.IsBanned ? httpHandler.BanTime : udpHandler.BanTime).ToString();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
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
            logger.Error(ex, ex.ToString());
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
            if (anime == null)
            {
                return null;
            }

            //AniDB
            contract.AniDB_ID = animeID;
            contract.AniDB_URL = string.Format(Constants.URLS.AniDB_Series, animeID);
            contract.AniDB_DiscussURL = string.Format(Constants.URLS.AniDB_SeriesDiscussion, animeID);

            // MAL
            var malRef = anime.GetCrossRefMAL();
            if (malRef != null && malRef.Count > 0)
            {
                contract.MAL_ID = malRef[0].MALID.ToString();
                contract.MAL_URL = string.Format(Constants.URLS.MAL_Series, malRef[0].MALID);
                //contract.MAL_DiscussURL = string.Format(Constants.URLS.MAL_SeriesDiscussion, malRef[0].MALID, malRef[0].MALTitle);
                contract.MAL_DiscussURL = string.Format(Constants.URLS.MAL_Series, malRef[0].MALID);
            }

            // TvDB
            var tvdbRef = anime.GetCrossRefTvDB();
            if (tvdbRef != null && tvdbRef.Count > 0)
            {
                contract.TvDB_ID = tvdbRef[0].TvDBID.ToString();
                contract.TvDB_URL = string.Format(Constants.URLS.TvDB_Series, tvdbRef[0].TvDBID);
            }

            // Trakt
            var traktRef = anime.GetCrossRefTraktV2();
            if (traktRef != null && traktRef.Count > 0)
            {
                contract.Trakt_ID = traktRef[0].TraktID;
                contract.Trakt_URL = string.Format(Constants.URLS.Trakt_Series, traktRef[0].TraktID);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
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
            logger.Error(ex, ex.ToString());
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
            logger.Error(ex, ex.ToString());
        }

        return new List<JMMUser>();
    }

    [HttpGet("Group/{userID}")]
    public List<CL_AnimeGroup_User> GetAllGroups(int userID)
    {
        try
        {
            return RepoFactory.AnimeGroup.GetAll()
                .Select(a => a.GetUserContract(userID))
                .OrderBy(a => a.SortName)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return new List<CL_AnimeGroup_User>();
    }

    [NonAction]
    public List<CL_AnimeEpisode_User> GetEpisodesRecentlyAddedSummary(int maxRecords, int jmmuserID)
    {
        var retEps = new List<CL_AnimeEpisode_User>();
        try
        {
            {
                var user = RepoFactory.JMMUser.GetByID(jmmuserID);
                if (user == null)
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

                var results = RepoFactory.VideoLocal.GetMostRecentlyAdded(maxRecords, jmmuserID)
                    .SelectMany(a => a.GetAnimeEpisodes()).GroupBy(a => a.AnimeSeriesID)
                    .Select(a => (a.Key, a.Max(b => b.DateTimeUpdated)));

                var numEps = 0;
                foreach ((var animeSeriesID, var lastUpdated) in results)
                {
                    var ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                    if (ser == null)
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

                    var eps = vids[0].GetAnimeEpisodes();
                    if (eps.Count == 0)
                    {
                        continue;
                    }

                    var epContract = eps[0].GetUserContract(jmmuserID);
                    if (epContract != null)
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
            logger.Error(ex, ex.ToString());
        }

        return retEps;
    }

    [HttpGet("Anime/New/{maxRecords}/{userID}")]
    public List<Metro_Anime_Summary> GetAnimeWithNewEpisodes(int maxRecords, int jmmuserID)
    {
        var retAnime = new List<Metro_Anime_Summary>();
        try
        {
            {
                var user = RepoFactory.JMMUser.GetByID(jmmuserID);
                if (user == null)
                {
                    return retAnime;
                }

                var results = RepoFactory.VideoLocal.GetMostRecentlyAdded(maxRecords, jmmuserID)
                    .SelectMany(a => a.GetAnimeEpisodes()).GroupBy(a => a.AnimeSeriesID)
                    .Select(a => (a.Key, a.Max(b => b.DateTimeUpdated)));

                var numEps = 0;
                foreach ((var animeSeriesID, var lastUpdated) in results)
                {
                    var ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                    if (ser == null)
                    {
                        continue;
                    }

                    if (!user.AllowedSeries(ser))
                    {
                        continue;
                    }

                    var serUser = ser.GetUserRecord(jmmuserID);

                    var vids =
                        RepoFactory.VideoLocal.GetMostRecentlyAddedForAnime(1, ser.AniDB_ID);
                    if (vids.Count == 0)
                    {
                        continue;
                    }

                    var eps = vids[0].GetAnimeEpisodes();
                    if (eps.Count == 0)
                    {
                        continue;
                    }

                    var epContract = eps[0].GetUserContract(jmmuserID);
                    if (epContract != null)
                    {
                        var anidb_anime = ser.GetAnime();

                        var summ = new Metro_Anime_Summary
                        {
                            AnimeID = ser.AniDB_ID,
                            AnimeName = ser.GetSeriesName(),
                            AnimeSeriesID = ser.AnimeSeriesID,
                            BeginYear = anidb_anime.BeginYear,
                            EndYear = anidb_anime.EndYear
                        };
                        //summ.PosterName = anidb_anime.GetDefaultPosterPathNoBlanks(session);
                        if (serUser != null)
                        {
                            summ.UnwatchedEpisodeCount = serUser.UnwatchedEpisodeCount;
                        }
                        else
                        {
                            summ.UnwatchedEpisodeCount = 0;
                        }

                        var imgDet = anidb_anime.GetDefaultPosterDetailsNoBlanks();
                        summ.ImageType = (int)imgDet.ImageType;
                        summ.ImageID = imgDet.ImageID;

                        retAnime.Add(summ);
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
            logger.Error(ex, ex.ToString());
        }

        return retAnime;
    }

    [NonAction]
    public List<Metro_Anime_Summary> GetAnimeContinueWatching_old(int maxRecords, int jmmuserID)
    {
        var retAnime = new List<Metro_Anime_Summary>();
        try
        {
            {
                var start = DateTime.Now;

                var user = RepoFactory.JMMUser.GetByID(jmmuserID);
                if (user == null)
                {
                    return retAnime;
                }

                // get a list of series that is applicable
                var allSeriesUser =
                    RepoFactory.AnimeSeries_User.GetMostRecentlyWatched(jmmuserID);

                var ts = DateTime.Now - start;
                logger.Info(string.Format("GetAnimeContinueWatching:Series: {0}", ts.TotalMilliseconds));

                foreach (var userRecord in allSeriesUser)
                {
                    start = DateTime.Now;

                    var series = RepoFactory.AnimeSeries.GetByID(userRecord.AnimeSeriesID);
                    if (series == null)
                    {
                        continue;
                    }

                    if (!user.AllowedSeries(series))
                    {
                        logger.Info(string.Format("GetAnimeContinueWatching:Skipping Anime - not allowed: {0}",
                            series.AniDB_ID));
                        continue;
                    }

                    var serUser = series.GetUserRecord(jmmuserID);

                    var ep = _service.GetNextUnwatchedEpisode(userRecord.AnimeSeriesID,
                        jmmuserID);
                    if (ep != null)
                    {
                        var anidb_anime = series.GetAnime();

                        var summ = new Metro_Anime_Summary
                        {
                            AnimeID = series.AniDB_ID,
                            AnimeName = series.GetSeriesName(),
                            AnimeSeriesID = series.AnimeSeriesID,
                            BeginYear = anidb_anime.BeginYear,
                            EndYear = anidb_anime.EndYear
                        };
                        //summ.PosterName = anidb_anime.GetDefaultPosterPathNoBlanks(session);

                        if (serUser != null)
                        {
                            summ.UnwatchedEpisodeCount = serUser.UnwatchedEpisodeCount;
                        }
                        else
                        {
                            summ.UnwatchedEpisodeCount = 0;
                        }

                        var imgDet = anidb_anime.GetDefaultPosterDetailsNoBlanks();
                        summ.ImageType = (int)imgDet.ImageType;
                        summ.ImageID = imgDet.ImageID;

                        retAnime.Add(summ);

                        ts = DateTime.Now - start;
                        logger.Info(string.Format("GetAnimeContinueWatching:Anime: {0} - {1}", summ.AnimeName,
                            ts.TotalMilliseconds));

                        // Lets only return the specified amount
                        if (retAnime.Count == maxRecords)
                        {
                            return retAnime;
                        }
                    }
                    else
                    {
                        logger.Info(string.Format("GetAnimeContinueWatching:Skipping Anime - no episodes: {0}",
                            series.AniDB_ID));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return retAnime;
    }

    [HttpGet("Anime/ContinueWatch/{maxRecords}/{userID}")]
    public List<Metro_Anime_Summary> GetAnimeContinueWatching(int maxRecords, int jmmuserID)
    {
        var retAnime = new List<Metro_Anime_Summary>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(jmmuserID);
            if (user == null)
            {
                return retAnime;
            }

            // find the locked Continue Watching Filter
            SVR_GroupFilter gf = null;
            var lockedGFs = RepoFactory.GroupFilter.GetLockedGroupFilters();
            if (lockedGFs != null)
            {
                // if it already exists we can leave
                foreach (var gfTemp in lockedGFs)
                {
                    if (gfTemp.FilterType == (int)GroupFilterType.ContinueWatching)
                    {
                        gf = gfTemp;
                        break;
                    }
                }
            }

            if (gf == null || !gf.GroupsIds.ContainsKey(jmmuserID))
            {
                return retAnime;
            }

            var comboGroups =
                gf.GroupsIds[jmmuserID]
                    .Select(a => RepoFactory.AnimeGroup.GetByID(a))
                    .Where(a => a != null)
                    .Select(a => a.GetUserContract(jmmuserID));

            // apply sorting
            comboGroups = GroupFilterHelper.Sort(comboGroups, gf);

            foreach (var grp in comboGroups)
            {
                foreach (var ser in RepoFactory.AnimeSeries.GetByGroupID(grp.AnimeGroupID))
                {
                    if (!user.AllowedSeries(ser))
                    {
                        continue;
                    }

                    var serUser = ser.GetUserRecord(jmmuserID);

                    var ep = _service.GetNextUnwatchedEpisode(ser.AnimeSeriesID, jmmuserID);
                    if (ep != null)
                    {
                        var anidb_anime = ser.GetAnime();

                        var summ = new Metro_Anime_Summary
                        {
                            AnimeID = ser.AniDB_ID,
                            AnimeName = ser.GetSeriesName(),
                            AnimeSeriesID = ser.AnimeSeriesID,
                            BeginYear = anidb_anime.BeginYear,
                            EndYear = anidb_anime.EndYear
                        };
                        //summ.PosterName = anidb_anime.GetDefaultPosterPathNoBlanks(session);

                        if (serUser != null)
                        {
                            summ.UnwatchedEpisodeCount = serUser.UnwatchedEpisodeCount;
                        }
                        else
                        {
                            summ.UnwatchedEpisodeCount = 0;
                        }

                        var imgDet = anidb_anime.GetDefaultPosterDetailsNoBlanks();
                        summ.ImageType = (int)imgDet.ImageType;
                        summ.ImageID = imgDet.ImageID;

                        retAnime.Add(summ);


                        // Lets only return the specified amount
                        if (retAnime.Count == maxRecords)
                        {
                            return retAnime;
                        }
                    }
                    else
                    {
                        logger.Info(string.Format("GetAnimeContinueWatching:Skipping Anime - no episodes: {0}",
                            ser.AniDB_ID));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return retAnime;
    }

    [HttpGet("Anime/Calendar/{userID}/{startDateSecs}/{endDateSecs}/{maxRecords}")]
    public List<Metro_Anime_Summary> GetAnimeCalendar(int jmmuserID, int startDateSecs, int endDateSecs,
        int maxRecords)
    {
        var retAnime = new List<Metro_Anime_Summary>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(jmmuserID);
            if (user == null)
            {
                return retAnime;
            }

            var startDate = AniDB.GetAniDBDateAsDate(startDateSecs);
            var endDate = AniDB.GetAniDBDateAsDate(endDateSecs);

            var animes =
                RepoFactory.AniDB_Anime.GetForDate(startDate.Value, endDate.Value);
            foreach (var anidb_anime in animes)
            {
                if (!user.AllowedAnime(anidb_anime))
                {
                    continue;
                }

                var ser = RepoFactory.AnimeSeries.GetByAnimeID(anidb_anime.AnimeID);

                var summ = new Metro_Anime_Summary
                {
                    AirDateAsSeconds = anidb_anime.GetAirDateAsSeconds(), AnimeID = anidb_anime.AnimeID
                };
                if (ser != null)
                {
                    summ.AnimeName = ser.GetSeriesName();
                    summ.AnimeSeriesID = ser.AnimeSeriesID;
                }
                else
                {
                    summ.AnimeName = anidb_anime.MainTitle;
                    summ.AnimeSeriesID = 0;
                }

                summ.BeginYear = anidb_anime.BeginYear;
                summ.EndYear = anidb_anime.EndYear;
                summ.PosterName = anidb_anime.GetDefaultPosterPathNoBlanks();

                var imgDet = anidb_anime.GetDefaultPosterDetailsNoBlanks();
                summ.ImageType = (int)imgDet.ImageType;
                summ.ImageID = imgDet.ImageID;

                retAnime.Add(summ);
                if (retAnime.Count == maxRecords)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return retAnime;
    }

    [HttpGet("Anime/Search/{userID}/{queryText}/{maxRecords}")]
    public List<Metro_Anime_Summary> SearchAnime(int jmmuserID, string queryText, int maxRecords)
    {
        var retAnime = new List<Metro_Anime_Summary>();
        try
        {
            var user = RepoFactory.JMMUser.GetByID(jmmuserID);
            if (user == null)
            {
                return retAnime;
            }


            var animes = RepoFactory.AniDB_Anime.SearchByName(queryText);
            foreach (var anidb_anime in animes)
            {
                if (!user.AllowedAnime(anidb_anime))
                {
                    continue;
                }

                var ser = RepoFactory.AnimeSeries.GetByAnimeID(anidb_anime.AnimeID);

                var summ = new Metro_Anime_Summary
                {
                    AirDateAsSeconds = anidb_anime.GetAirDateAsSeconds(), AnimeID = anidb_anime.AnimeID
                };
                if (ser != null)
                {
                    summ.AnimeName = ser.GetSeriesName();
                    summ.AnimeSeriesID = ser.AnimeSeriesID;
                }
                else
                {
                    summ.AnimeName = anidb_anime.MainTitle;
                    summ.AnimeSeriesID = 0;
                }

                summ.BeginYear = anidb_anime.BeginYear;
                summ.EndYear = anidb_anime.EndYear;
                summ.PosterName = anidb_anime.GetDefaultPosterPathNoBlanks();

                var imgDet = anidb_anime.GetDefaultPosterDetailsNoBlanks();
                summ.ImageType = (int)imgDet.ImageType;
                summ.ImageID = imgDet.ImageID;

                retAnime.Add(summ);
                if (retAnime.Count == maxRecords)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return retAnime;
    }

    [HttpGet("Anime/Detail/{animeID}/{userID}/{maxEpisodeRecords}")]
    public Metro_Anime_Detail GetAnimeDetail(int animeID, int jmmuserID, int maxEpisodeRecords)
    {
        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            if (anime == null)
            {
                return null;
            }

            var ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

            var ret = new Metro_Anime_Detail { AnimeID = anime.AnimeID };
            if (ser != null)
            {
                ret.AnimeName = ser.GetSeriesName();
            }
            else
            {
                ret.AnimeName = anime.MainTitle;
            }

            if (ser != null)
            {
                ret.AnimeSeriesID = ser.AnimeSeriesID;
            }
            else
            {
                ret.AnimeSeriesID = 0;
            }

            ret.BeginYear = anime.BeginYear;
            ret.EndYear = anime.EndYear;

            var imgDet = anime.GetDefaultPosterDetailsNoBlanks();
            ret.PosterImageType = (int)imgDet.ImageType;
            ret.PosterImageID = imgDet.ImageID;

            var imgDetFan = anime.GetDefaultFanartDetailsNoBlanks();
            if (imgDetFan != null)
            {
                ret.FanartImageType = (int)imgDetFan.ImageType;
                ret.FanartImageID = imgDetFan.ImageID;
            }
            else
            {
                ret.FanartImageType = 0;
                ret.FanartImageID = 0;
            }

            ret.AnimeType = anime.GetAnimeTypeDescription();
            ret.Description = anime.Description;
            ret.EpisodeCountNormal = anime.EpisodeCountNormal;
            ret.EpisodeCountSpecial = anime.EpisodeCountSpecial;


            ret.AirDate = anime.AirDate;
            ret.EndDate = anime.EndDate;

            ret.OverallRating = anime.GetAniDBRating();
            ret.TotalVotes = anime.GetAniDBTotalVotes();
            ret.AllTags = anime.TagsString;

            ret.NextEpisodesToWatch = new List<Metro_Anime_Episode>();
            if (ser != null)
            {
                var serUserRec = ser.GetUserRecord(jmmuserID);
                if (ser != null)
                {
                    ret.UnwatchedEpisodeCount = serUserRec.UnwatchedEpisodeCount;
                }
                else
                {
                    ret.UnwatchedEpisodeCount = 0;
                }


                var epList = new List<SVR_AnimeEpisode>();
                var dictEpUsers =
                    new Dictionary<int, SVR_AnimeEpisode_User>();
                foreach (
                    var userRecord in
                    RepoFactory.AnimeEpisode_User.GetByUserIDAndSeriesID(jmmuserID, ser.AnimeSeriesID))
                {
                    dictEpUsers[userRecord.AnimeEpisodeID] = userRecord;
                }

                foreach (var animeep in RepoFactory.AnimeEpisode.GetBySeriesID(ser.AnimeSeriesID))
                {
                    if (!dictEpUsers.ContainsKey(animeep.AnimeEpisodeID))
                    {
                        epList.Add(animeep);
                        continue;
                    }

                    var usrRec = dictEpUsers[animeep.AnimeEpisodeID];
                    if (usrRec.WatchedCount == 0 || !usrRec.WatchedDate.HasValue)
                    {
                        epList.Add(animeep);
                    }
                }

                var aniEpList = RepoFactory.AniDB_Episode.GetByAnimeID(ser.AniDB_ID);
                var dictAniEps = new Dictionary<int, AniDB_Episode>();
                foreach (var aniep in aniEpList)
                {
                    dictAniEps[aniep.EpisodeID] = aniep;
                }

                var candidateEps = new List<CL_AnimeEpisode_User>();

                foreach (var ep in epList)
                {
                    if (dictAniEps.ContainsKey(ep.AniDB_EpisodeID))
                    {
                        var anidbep = dictAniEps[ep.AniDB_EpisodeID];
                        if (anidbep.EpisodeType == (int)EpisodeType.Episode ||
                            anidbep.EpisodeType == (int)EpisodeType.Special)
                        {
                            // The episode list have already been filtered to only episodes with a user record
                            // So just add the candidate to the list.
                            candidateEps.Add(ep.GetUserContract(jmmuserID));
                        }
                    }
                }

                if (candidateEps.Count > 0)
                {
                    var tvSummary = new TvDBSummary();
                    tvSummary.Populate(ser.AniDB_ID);

                    // sort by episode type and number to find the next episode

                    // this will generate a lot of queries when the user doesn have files
                    // for these episodes
                    var cnt = 0;
                    foreach (var canEp in candidateEps.OrderBy(a => a.EpisodeType)
                                 .ThenBy(a => a.EpisodeNumber))
                    {
                        if (dictAniEps.ContainsKey(canEp.AniDB_EpisodeID))
                        {
                            var anidbep = dictAniEps[canEp.AniDB_EpisodeID];

                            SVR_AnimeEpisode_User userEpRecord = null;
                            if (dictEpUsers.ContainsKey(canEp.AnimeEpisodeID))
                            {
                                userEpRecord = dictEpUsers[canEp.AnimeEpisodeID];
                            }

                            // now refresh from the database to get file count
                            var epFresh = RepoFactory.AnimeEpisode.GetByID(canEp.AnimeEpisodeID);

                            var fileCount = epFresh.GetVideoLocals().Count;
                            if (fileCount > 0)
                            {
                                var contract = new Metro_Anime_Episode
                                {
                                    AnimeEpisodeID = epFresh.AnimeEpisodeID, LocalFileCount = fileCount
                                };
                                if (userEpRecord == null)
                                {
                                    contract.IsWatched = false;
                                }
                                else
                                {
                                    contract.IsWatched = userEpRecord.WatchedCount > 0;
                                }

                                // anidb
                                contract.EpisodeNumber = anidbep.EpisodeNumber;
                                contract.EpisodeName = epFresh.Title;

                                contract.EpisodeType = anidbep.EpisodeType;
                                contract.LengthSeconds = anidbep.LengthSeconds;
                                contract.AirDate = anidbep.GetAirDateFormatted();

                                // tvdb
                                SetTvDBInfo(tvSummary, anidbep, ref contract);


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
            logger.Error(ex, ex.ToString());
            return null;
        }
    }

    [HttpGet("Anime/Summary/{animeID}")]
    public Metro_Anime_Summary GetAnimeSummary(int animeID)
    {
        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            if (anime == null)
            {
                return null;
            }

            var ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

            var summ = new Metro_Anime_Summary
            {
                AnimeID = anime.AnimeID,
                AnimeName = anime.MainTitle,
                AnimeSeriesID = 0,
                BeginYear = anime.BeginYear,
                EndYear = anime.EndYear,
                PosterName = anime.GetDefaultPosterPathNoBlanks()
            };
            var imgDet = anime.GetDefaultPosterDetailsNoBlanks();
            summ.ImageType = (int)imgDet.ImageType;
            summ.ImageID = imgDet.ImageID;

            if (ser != null)
            {
                summ.AnimeName = ser.GetSeriesName();
                summ.AnimeSeriesID = ser.AnimeSeriesID;
            }

            return summ;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return null;
    }

    [NonAction]
    public static void SetTvDBInfo(SVR_AniDB_Anime anime, AniDB_Episode ep, ref Metro_Anime_Episode contract)
    {
        var tvSummary = new TvDBSummary();
        tvSummary.Populate(anime.AnimeID);

        SetTvDBInfo(tvSummary, ep, ref contract);
    }

    [NonAction]
    public static void SetTvDBInfo(int anidbid, AniDB_Episode ep, ref Metro_Anime_Episode contract)
    {
        var tvSummary = new TvDBSummary();
        tvSummary.Populate(anidbid);

        SetTvDBInfo(tvSummary, ep, ref contract);
    }

    [NonAction]
    public static void SetTvDBInfo(TvDBSummary tvSummary, AniDB_Episode ep, ref Metro_Anime_Episode contract)
    {
        var override_link = RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAniDBEpisodeID(ep.EpisodeID);
        if (override_link.Any(a => a != null))
        {
            var tvep = RepoFactory.TvDB_Episode.GetByTvDBID(override_link.FirstOrDefault().TvDBEpisodeID);
            contract.EpisodeName = tvep.EpisodeName;
            contract.EpisodeOverview = tvep.Overview;
            contract.ImageID = tvep.Id;
            contract.ImageType = (int)ImageEntityType.TvDB_Episode;
            return;
        }

        var link = RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAniDBEpisodeID(ep.EpisodeID);
        if (link.Any(a => a != null))
        {
            var tvep = RepoFactory.TvDB_Episode.GetByTvDBID(link.FirstOrDefault().TvDBEpisodeID);
            contract.EpisodeName = tvep.EpisodeName;
            contract.EpisodeOverview = tvep.Overview;
            contract.ImageID = tvep.Id;
            contract.ImageType = (int)ImageEntityType.TvDB_Episode;
        }
    }

    [HttpGet("Anime/Character/{animeID}/{maxRecords}")]
    public List<Metro_AniDB_Character> GetCharactersForAnime(int animeID, int maxRecords)
    {
        var chars = new List<Metro_AniDB_Character>();

        try
        {
            var animeChars =
                RepoFactory.AniDB_Anime_Character.GetByAnimeID(animeID);
            if (animeChars == null || animeChars.Count == 0)
            {
                return chars;
            }

            var cnt = 0;

            // first get all the main characters
            foreach (
                var animeChar in
                animeChars.Where(
                    item =>
                        item.CharType.Equals("main character in",
                            StringComparison.InvariantCultureIgnoreCase)))
            {
                cnt++;
                var chr = RepoFactory.AniDB_Character.GetByID(animeChar.CharID);
                if (chr != null)
                {
                    var contract = new Metro_AniDB_Character();
                    chars.Add(chr.ToContractMetro(animeChar));
                }

                if (cnt == maxRecords)
                {
                    break;
                }
            }

            // now get the rest
            foreach (
                var animeChar in
                animeChars.Where(
                    item =>
                        !item.CharType.Equals("main character in",
                            StringComparison.InvariantCultureIgnoreCase))
            )
            {
                cnt++;
                var chr = RepoFactory.AniDB_Character.GetByID(animeChar.CharID);
                if (chr != null)
                {
                    var contract = new Metro_AniDB_Character();
                    chars.Add(chr.ToContractMetro(animeChar));
                }

                if (cnt == maxRecords)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return chars;
    }

    [HttpGet("Anime/Comment/{animeID}/{maxRecords}")]
    public List<Metro_Comment> GetTraktCommentsForAnime(int animeID, int maxRecords)
    {
        return new List<Metro_Comment>();
    }

    [HttpGet("Anime/Recommendation/{animeID}/{maxRecords}")]
    public List<Metro_Comment> GetAniDBRecommendationsForAnime(int animeID, int maxRecords)
    {
        return new List<Metro_Comment>();
    }

    [HttpGet("Anime/Similar/{animeID}/{maxRecords}/{userID}")]
    public List<Metro_Anime_Summary> GetSimilarAnimeForAnime(int animeID, int maxRecords, int jmmuserID)
    {
        var links = new List<CL_AniDB_Anime_Similar>();
        var retAnime = new List<Metro_Anime_Summary>();
        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            if (anime == null)
            {
                return retAnime;
            }

            var juser = RepoFactory.JMMUser.GetByID(jmmuserID);
            if (juser == null)
            {
                return retAnime;
            }


            // first get the related anime
            foreach (AniDB_Anime_Relation link in anime.GetRelatedAnime())
            {
                var animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(link.RelatedAnimeID);

                if (animeLink == null)
                {
                    // try getting it from anidb now
                    var command = _commandFactory.Create<CommandRequest_GetAnimeHTTP>(c =>
                    {
                        c.BubbleExceptions = true;
                        c.DownloadRelations = false;
                        c.AnimeID = link.RelatedAnimeID;
                        c.CreateSeriesEntry = false;
                    });
                    command.ProcessCommand();
                    animeLink = command.Result;
                }

                if (animeLink == null)
                {
                    continue;
                }

                if (!juser.AllowedAnime(animeLink))
                {
                    continue;
                }

                // check if this anime has a series
                var ser = RepoFactory.AnimeSeries.GetByAnimeID(link.RelatedAnimeID);

                var summ = new Metro_Anime_Summary
                {
                    AnimeID = animeLink.AnimeID,
                    AnimeName = animeLink.MainTitle,
                    AnimeSeriesID = 0,
                    BeginYear = animeLink.BeginYear,
                    EndYear = animeLink.EndYear,
                    //summ.PosterName = animeLink.GetDefaultPosterPathNoBlanks(session);

                    RelationshipType = link.RelationType
                };
                var imgDet = animeLink.GetDefaultPosterDetailsNoBlanks();
                summ.ImageType = (int)imgDet.ImageType;
                summ.ImageID = imgDet.ImageID;

                if (ser != null)
                {
                    summ.AnimeName = ser.GetSeriesName();
                    summ.AnimeSeriesID = ser.AnimeSeriesID;
                }

                retAnime.Add(summ);
            }

            // now get similar anime
            foreach (var link in anime.GetSimilarAnime())
            {
                var animeLink =
                    RepoFactory.AniDB_Anime.GetByAnimeID(link.SimilarAnimeID);

                if (animeLink == null)
                {
                    // try getting it from anidb now
                    var command = _commandFactory.Create<CommandRequest_GetAnimeHTTP>(
                        c =>
                        {
                            c.BubbleExceptions = true;
                            c.DownloadRelations = false;
                            c.AnimeID = link.SimilarAnimeID;
                            c.CreateSeriesEntry = false;
                        }
                    );
                    command.ProcessCommand();
                    animeLink = command.Result;
                }

                if (animeLink == null)
                {
                    continue;
                }

                if (!juser.AllowedAnime(animeLink))
                {
                    continue;
                }

                // check if this anime has a series
                var ser = RepoFactory.AnimeSeries.GetByAnimeID(link.SimilarAnimeID);

                var summ = new Metro_Anime_Summary
                {
                    AnimeID = animeLink.AnimeID,
                    AnimeName = animeLink.MainTitle,
                    AnimeSeriesID = 0,
                    BeginYear = animeLink.BeginYear,
                    EndYear = animeLink.EndYear,
                    //summ.PosterName = animeLink.GetDefaultPosterPathNoBlanks(session);

                    RelationshipType = "Recommendation"
                };
                var imgDet = animeLink.GetDefaultPosterDetailsNoBlanks();
                summ.ImageType = (int)imgDet.ImageType;
                summ.ImageID = imgDet.ImageID;

                if (ser != null)
                {
                    summ.AnimeName = ser.GetSeriesName();
                    summ.AnimeSeriesID = ser.AnimeSeriesID;
                }

                retAnime.Add(summ);

                if (retAnime.Count == maxRecords)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return retAnime;
    }

    [HttpGet("Episode/Files/{episodeID}/{userID}")]
    public List<CL_VideoDetailed> GetFilesForEpisode(int episodeID, int userID)
    {
        try
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(episodeID);
            return ep != null
                ? ep.GetVideoDetailedContracts(userID)
                : new List<CL_VideoDetailed>();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return new List<CL_VideoDetailed>();
    }

    [HttpGet("Episode/Watch/{animeEpisodeID}/{watchedStatus}/{userID}")]
    public CL_Response<CL_AnimeEpisode_User> ToggleWatchedStatusOnEpisode(int animeEpisodeID,
        bool watchedStatus, int userID)
    {
        var response =
            new CL_Response<CL_AnimeEpisode_User> { ErrorMessage = string.Empty, Result = null };
        try
        {
            var ep = RepoFactory.AnimeEpisode.GetByID(animeEpisodeID);
            if (ep == null)
            {
                response.ErrorMessage = "Could not find anime episode record";
                return response;
            }

            ep.ToggleWatchedStatus(watchedStatus, true, DateTime.Now, false, userID, true);
            var series = ep.GetAnimeSeries();
            series?.UpdateStats(true, false);
            series?.AnimeGroup?.TopLevelAnimeGroup?.UpdateStatsFromTopLevel(true, true);

            // refresh from db
            ep = RepoFactory.AnimeEpisode.GetByID(animeEpisodeID);

            response.Result = ep.GetUserContract(userID);

            return response;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            response.ErrorMessage = ex.Message;
            return response;
        }
    }

    [HttpGet("Anime/Refresh/{animeID}")]
    public string UpdateAnimeData(int animeID)
    {
        try
        {
            var command = _commandFactory.Create<CommandRequest_GetAnimeHTTP>(c =>
            {
                c.BubbleExceptions = true;
                c.ForceRefresh = true;
                c.DownloadRelations = false;
                c.AnimeID = animeID;
                c.CreateSeriesEntry = false;
            });
            command.ProcessCommand();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return string.Empty;
    }
}
