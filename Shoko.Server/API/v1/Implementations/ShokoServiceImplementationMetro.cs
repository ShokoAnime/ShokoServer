using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AniDBAPI;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Commons.Utils;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Metro;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.ImageDownload;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Providers.TraktTV.Contracts;
using Shoko.Models.TvDB;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server
{
    public class ShokoServiceImplementationMetro : IShokoServerMetro
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public CL_ServerStatus GetServerStatus()
        {
            CL_ServerStatus contract = new CL_ServerStatus();

            try
            {
                contract.HashQueueCount = ShokoService.CmdProcessorHasher.QueueCount;
                contract.HashQueueState =
                    ShokoService.CmdProcessorHasher.QueueState.formatMessage(); //Deprecated since 3.6.0.0
                contract.HashQueueStateId = (int) ShokoService.CmdProcessorHasher.QueueState.queueState;
                contract.HashQueueStateParams = ShokoService.CmdProcessorHasher.QueueState.extraParams;

                contract.GeneralQueueCount = ShokoService.CmdProcessorGeneral.QueueCount;
                contract.GeneralQueueState =
                    ShokoService.CmdProcessorGeneral.QueueState.formatMessage(); //Deprecated since 3.6.0.0
                contract.GeneralQueueStateId = (int) ShokoService.CmdProcessorGeneral.QueueState.queueState;
                contract.GeneralQueueStateParams = ShokoService.CmdProcessorGeneral.QueueState.extraParams;

                contract.ImagesQueueCount = ShokoService.CmdProcessorImages.QueueCount;
                contract.ImagesQueueState =
                    ShokoService.CmdProcessorImages.QueueState.formatMessage(); //Deprecated since 3.6.0.0
                contract.ImagesQueueStateId = (int) ShokoService.CmdProcessorImages.QueueState.queueState;
                contract.ImagesQueueStateParams = ShokoService.CmdProcessorImages.QueueState.extraParams;

                contract.IsBanned = ShokoService.AnidbProcessor.IsBanned;
                contract.BanReason = ShokoService.AnidbProcessor.BanTime.ToString();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return contract;
        }

        public CL_ServerSettings GetServerSettings()
        {
            CL_ServerSettings contract = new CL_ServerSettings();

            try
            {
                return ServerSettings.ToContract();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return contract;
        }

        public CL_Response<bool> PostCommentShow(string traktID, string commentText, bool isSpoiler)
        {
            return TraktTVHelper.PostCommentShow(traktID, commentText, isSpoiler);
        }

        public Metro_CommunityLinks GetCommunityLinks(int animeID)
        {
            Metro_CommunityLinks contract = new Metro_CommunityLinks();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();


                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, animeID);
                    if (anime == null) return null;

                    //AniDB
                    contract.AniDB_ID = animeID;
                    contract.AniDB_URL = string.Format(Constants.URLS.AniDB_Series, animeID);
                    contract.AniDB_DiscussURL = string.Format(Constants.URLS.AniDB_SeriesDiscussion, animeID);

                    // MAL
                    List<CrossRef_AniDB_MAL> malRef = anime.GetCrossRefMAL(session);
                    if (malRef != null && malRef.Count > 0)
                    {
                        contract.MAL_ID = malRef[0].MALID.ToString();
                        contract.MAL_URL = string.Format(Constants.URLS.MAL_Series, malRef[0].MALID);
                        //contract.MAL_DiscussURL = string.Format(Constants.URLS.MAL_SeriesDiscussion, malRef[0].MALID, malRef[0].MALTitle);
                        contract.MAL_DiscussURL = string.Format(Constants.URLS.MAL_Series, malRef[0].MALID);
                    }

                    // TvDB
                    List<CrossRef_AniDB_TvDBV2> tvdbRef = anime.GetCrossRefTvDBV2();
                    if (tvdbRef != null && tvdbRef.Count > 0)
                    {
                        contract.TvDB_ID = tvdbRef[0].TvDBID.ToString();
                        contract.TvDB_URL = string.Format(Constants.URLS.TvDB_Series, tvdbRef[0].TvDBID);
                    }

                    // Trakt
                    List<CrossRef_AniDB_TraktV2> traktRef = anime.GetCrossRefTraktV2(session);
                    if (traktRef != null && traktRef.Count > 0)
                    {
                        contract.Trakt_ID = traktRef[0].TraktID;
                        contract.Trakt_URL = string.Format(Constants.URLS.Trakt_Series, traktRef[0].TraktID);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return contract;
        }

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

        public List<CL_AnimeEpisode_User> GetEpisodesRecentlyAddedSummary(int maxRecords, int jmmuserID)
        {
            List<CL_AnimeEpisode_User> retEps = new List<CL_AnimeEpisode_User>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retEps;

                    string sql = "Select ae.AnimeSeriesID, max(vl.DateTimeCreated) as MaxDate " +
                                 "From VideoLocal vl " +
                                 "INNER JOIN CrossRef_File_Episode xref ON vl.Hash = xref.Hash " +
                                 "INNER JOIN AnimeEpisode ae ON ae.AniDB_EpisodeID = xref.EpisodeID " +
                                 "GROUP BY ae.AnimeSeriesID " +
                                 "ORDER BY MaxDate desc ";
                    ArrayList results = DatabaseFactory.Instance.GetData(sql);

                    int numEps = 0;
                    foreach (object[] res in results)
                    {
                        int animeSeriesID = int.Parse(res[0].ToString());

                        SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                        if (ser == null) continue;

                        if (!user.AllowedSeries(ser)) continue;


                        List<SVR_VideoLocal> vids =
                            RepoFactory.VideoLocal.GetMostRecentlyAddedForAnime(1, ser.AniDB_ID);
                        if (vids.Count == 0) continue;

                        List<SVR_AnimeEpisode> eps = vids[0].GetAnimeEpisodes();
                        if (eps.Count == 0) continue;

                        CL_AnimeEpisode_User epContract = eps[0].GetUserContract(jmmuserID);
                        if (epContract != null)
                        {
                            retEps.Add(epContract);
                            numEps++;

                            // Lets only return the specified amount
                            if (retEps.Count == maxRecords) return retEps;
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

        public List<Metro_Anime_Summary> GetAnimeWithNewEpisodes(int maxRecords, int jmmuserID)
        {
            List<Metro_Anime_Summary> retAnime = new List<Metro_Anime_Summary>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();

                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retAnime;

                    string sql = "Select ae.AnimeSeriesID, max(vl.DateTimeCreated) as MaxDate " +
                                 "From VideoLocal vl " +
                                 "INNER JOIN CrossRef_File_Episode xref ON vl.Hash = xref.Hash " +
                                 "INNER JOIN AnimeEpisode ae ON ae.AniDB_EpisodeID = xref.EpisodeID " +
                                 "GROUP BY ae.AnimeSeriesID " +
                                 "ORDER BY MaxDate desc ";
                    ArrayList results = DatabaseFactory.Instance.GetData(sql);

                    int numEps = 0;
                    foreach (object[] res in results)
                    {
                        int animeSeriesID = int.Parse(res[0].ToString());

                        SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                        if (ser == null) continue;

                        if (!user.AllowedSeries(ser)) continue;

                        SVR_AnimeSeries_User serUser = ser.GetUserRecord(jmmuserID);

                        List<SVR_VideoLocal> vids =
                            RepoFactory.VideoLocal.GetMostRecentlyAddedForAnime(1, ser.AniDB_ID);
                        if (vids.Count == 0) continue;

                        List<SVR_AnimeEpisode> eps = vids[0].GetAnimeEpisodes();
                        if (eps.Count == 0) continue;

                        CL_AnimeEpisode_User epContract = eps[0].GetUserContract(jmmuserID);
                        if (epContract != null)
                        {
                            SVR_AniDB_Anime anidb_anime = ser.GetAnime();

                            Metro_Anime_Summary summ = new Metro_Anime_Summary
                            {
                                AnimeID = ser.AniDB_ID,
                                AnimeName = ser.GetSeriesName(),
                                AnimeSeriesID = ser.AnimeSeriesID,
                                BeginYear = anidb_anime.BeginYear,
                                EndYear = anidb_anime.EndYear
                            };
                            //summ.PosterName = anidb_anime.GetDefaultPosterPathNoBlanks(session);
                            if (serUser != null)
                                summ.UnwatchedEpisodeCount = serUser.UnwatchedEpisodeCount;
                            else
                                summ.UnwatchedEpisodeCount = 0;

                            ImageDetails imgDet = anidb_anime.GetDefaultPosterDetailsNoBlanks(sessionWrapper);
                            summ.ImageType = (int) imgDet.ImageType;
                            summ.ImageID = imgDet.ImageID;

                            retAnime.Add(summ);
                            numEps++;

                            // Lets only return the specified amount
                            if (retAnime.Count == maxRecords) return retAnime;
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

        public List<Metro_Anime_Summary> GetAnimeContinueWatching_old(int maxRecords, int jmmuserID)
        {
            List<Metro_Anime_Summary> retAnime = new List<Metro_Anime_Summary>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();

                    DateTime start = DateTime.Now;

                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retAnime;

                    // get a list of series that is applicable
                    List<SVR_AnimeSeries_User> allSeriesUser =
                        RepoFactory.AnimeSeries_User.GetMostRecentlyWatched(jmmuserID);

                    TimeSpan ts = DateTime.Now - start;
                    logger.Info(string.Format("GetAnimeContinueWatching:Series: {0}", ts.TotalMilliseconds));


                    ShokoServiceImplementation imp = new ShokoServiceImplementation();
                    foreach (SVR_AnimeSeries_User userRecord in allSeriesUser)
                    {
                        start = DateTime.Now;

                        SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByID(userRecord.AnimeSeriesID);
                        if (series == null) continue;

                        if (!user.AllowedSeries(series))
                        {
                            logger.Info(string.Format("GetAnimeContinueWatching:Skipping Anime - not allowed: {0}",
                                series.AniDB_ID));
                            continue;
                        }

                        SVR_AnimeSeries_User serUser = series.GetUserRecord(jmmuserID);

                        CL_AnimeEpisode_User ep = imp.GetNextUnwatchedEpisode(sessionWrapper, userRecord.AnimeSeriesID,
                            jmmuserID);
                        if (ep != null)
                        {
                            SVR_AniDB_Anime anidb_anime = series.GetAnime();

                            Metro_Anime_Summary summ = new Metro_Anime_Summary
                            {
                                AnimeID = series.AniDB_ID,
                                AnimeName = series.GetSeriesName(),
                                AnimeSeriesID = series.AnimeSeriesID,
                                BeginYear = anidb_anime.BeginYear,
                                EndYear = anidb_anime.EndYear
                            };
                            //summ.PosterName = anidb_anime.GetDefaultPosterPathNoBlanks(session);

                            if (serUser != null)
                                summ.UnwatchedEpisodeCount = serUser.UnwatchedEpisodeCount;
                            else
                                summ.UnwatchedEpisodeCount = 0;

                            ImageDetails imgDet = anidb_anime.GetDefaultPosterDetailsNoBlanks(sessionWrapper);
                            summ.ImageType = (int) imgDet.ImageType;
                            summ.ImageID = imgDet.ImageID;

                            retAnime.Add(summ);

                            ts = DateTime.Now - start;
                            logger.Info(string.Format("GetAnimeContinueWatching:Anime: {0} - {1}", summ.AnimeName,
                                ts.TotalMilliseconds));

                            // Lets only return the specified amount
                            if (retAnime.Count == maxRecords) return retAnime;
                        }
                        else
                            logger.Info(string.Format("GetAnimeContinueWatching:Skipping Anime - no episodes: {0}",
                                series.AniDB_ID));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return retAnime;
        }

        public List<Metro_Anime_Summary> GetAnimeContinueWatching(int maxRecords, int jmmuserID)
        {
            List<Metro_Anime_Summary> retAnime = new List<Metro_Anime_Summary>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();
                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retAnime;

                    // find the locked Continue Watching Filter
                    SVR_GroupFilter gf = null;
                    List<SVR_GroupFilter> lockedGFs = RepoFactory.GroupFilter.GetLockedGroupFilters();
                    if (lockedGFs != null)
                    {
                        // if it already exists we can leave
                        foreach (SVR_GroupFilter gfTemp in lockedGFs)
                        {
                            if (gfTemp.FilterType == (int) GroupFilterType.ContinueWatching)
                            {
                                gf = gfTemp;
                                break;
                            }
                        }
                    }
                    if ((gf == null) || !gf.GroupsIds.ContainsKey(jmmuserID))
                        return retAnime;
                    IEnumerable<CL_AnimeGroup_User> comboGroups =
                        gf.GroupsIds[jmmuserID]
                            .Select(a => RepoFactory.AnimeGroup.GetByID(a))
                            .Where(a => a != null)
                            .Select(a => a.GetUserContract(jmmuserID));

                    // apply sorting
                    comboGroups = GroupFilterHelper.Sort(comboGroups, gf);

                    foreach (CL_AnimeGroup_User grp in comboGroups)
                    {
                        ShokoServiceImplementation imp = new ShokoServiceImplementation();
                        foreach (SVR_AnimeSeries ser in RepoFactory.AnimeSeries.GetByGroupID(grp.AnimeGroupID))
                        {
                            if (!user.AllowedSeries(ser)) continue;

                            SVR_AnimeSeries_User serUser = ser.GetUserRecord(jmmuserID);

                            CL_AnimeEpisode_User ep =
                                imp.GetNextUnwatchedEpisode(sessionWrapper, ser.AnimeSeriesID, jmmuserID);
                            if (ep != null)
                            {
                                SVR_AniDB_Anime anidb_anime = ser.GetAnime();

                                Metro_Anime_Summary summ = new Metro_Anime_Summary
                                {
                                    AnimeID = ser.AniDB_ID,
                                    AnimeName = ser.GetSeriesName(),
                                    AnimeSeriesID = ser.AnimeSeriesID,
                                    BeginYear = anidb_anime.BeginYear,
                                    EndYear = anidb_anime.EndYear
                                };
                                //summ.PosterName = anidb_anime.GetDefaultPosterPathNoBlanks(session);

                                if (serUser != null)
                                    summ.UnwatchedEpisodeCount = serUser.UnwatchedEpisodeCount;
                                else
                                    summ.UnwatchedEpisodeCount = 0;

                                ImageDetails imgDet = anidb_anime.GetDefaultPosterDetailsNoBlanks(sessionWrapper);
                                summ.ImageType = (int) imgDet.ImageType;
                                summ.ImageID = imgDet.ImageID;

                                retAnime.Add(summ);


                                // Lets only return the specified amount
                                if (retAnime.Count == maxRecords) return retAnime;
                            }
                            else
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

        public List<Metro_Anime_Summary> GetAnimeCalendar(int jmmuserID, int startDateSecs, int endDateSecs,
            int maxRecords)
        {
            List<Metro_Anime_Summary> retAnime = new List<Metro_Anime_Summary>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();


                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retAnime;

                    DateTime? startDate = AniDB.GetAniDBDateAsDate(startDateSecs);
                    DateTime? endDate = AniDB.GetAniDBDateAsDate(endDateSecs);

                    List<SVR_AniDB_Anime> animes =
                        RepoFactory.AniDB_Anime.GetForDate(session, startDate.Value, endDate.Value);
                    foreach (SVR_AniDB_Anime anidb_anime in animes)
                    {
                        if (!user.AllowedAnime(anidb_anime)) continue;

                        SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(anidb_anime.AnimeID);

                        Metro_Anime_Summary summ = new Metro_Anime_Summary
                        {
                            AirDateAsSeconds = anidb_anime.GetAirDateAsSeconds(),
                            AnimeID = anidb_anime.AnimeID
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
                        summ.PosterName = anidb_anime.GetDefaultPosterPathNoBlanks(sessionWrapper);

                        ImageDetails imgDet = anidb_anime.GetDefaultPosterDetailsNoBlanks(sessionWrapper);
                        summ.ImageType = (int) imgDet.ImageType;
                        summ.ImageID = imgDet.ImageID;

                        retAnime.Add(summ);
                        if (retAnime.Count == maxRecords) break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return retAnime;
        }

        public List<Metro_Anime_Summary> SearchAnime(int jmmuserID, string queryText, int maxRecords)
        {
            List<Metro_Anime_Summary> retAnime = new List<Metro_Anime_Summary>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();


                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retAnime;


                    List<SVR_AniDB_Anime> animes = RepoFactory.AniDB_Anime.SearchByName(session, queryText);
                    foreach (SVR_AniDB_Anime anidb_anime in animes)
                    {
                        if (!user.AllowedAnime(anidb_anime)) continue;

                        SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(anidb_anime.AnimeID);

                        Metro_Anime_Summary summ = new Metro_Anime_Summary
                        {
                            AirDateAsSeconds = anidb_anime.GetAirDateAsSeconds(),
                            AnimeID = anidb_anime.AnimeID
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
                        summ.PosterName = anidb_anime.GetDefaultPosterPathNoBlanks(sessionWrapper);

                        ImageDetails imgDet = anidb_anime.GetDefaultPosterDetailsNoBlanks(sessionWrapper);
                        summ.ImageType = (int) imgDet.ImageType;
                        summ.ImageID = imgDet.ImageID;

                        retAnime.Add(summ);
                        if (retAnime.Count == maxRecords) break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return retAnime;
        }

        public Metro_Anime_Detail GetAnimeDetail(int animeID, int jmmuserID, int maxEpisodeRecords)
        {
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();


                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, animeID);
                    if (anime == null) return null;

                    SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

                    Metro_Anime_Detail ret = new Metro_Anime_Detail
                    {
                        AnimeID = anime.AnimeID
                    };
                    if (ser != null)
                        ret.AnimeName = ser.GetSeriesName();
                    else
                        ret.AnimeName = anime.MainTitle;

                    if (ser != null)
                        ret.AnimeSeriesID = ser.AnimeSeriesID;
                    else
                        ret.AnimeSeriesID = 0;

                    ret.BeginYear = anime.BeginYear;
                    ret.EndYear = anime.EndYear;

                    ImageDetails imgDet = anime.GetDefaultPosterDetailsNoBlanks(sessionWrapper);
                    ret.PosterImageType = (int) imgDet.ImageType;
                    ret.PosterImageID = imgDet.ImageID;

                    ImageDetails imgDetFan = anime.GetDefaultFanartDetailsNoBlanks(sessionWrapper);
                    if (imgDetFan != null)
                    {
                        ret.FanartImageType = (int) imgDetFan.ImageType;
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
                        SVR_AnimeSeries_User serUserRec = ser.GetUserRecord(jmmuserID);
                        if (ser != null)
                            ret.UnwatchedEpisodeCount = serUserRec.UnwatchedEpisodeCount;
                        else
                            ret.UnwatchedEpisodeCount = 0;


                        List<SVR_AnimeEpisode> epList = new List<SVR_AnimeEpisode>();
                        Dictionary<int, SVR_AnimeEpisode_User> dictEpUsers =
                            new Dictionary<int, SVR_AnimeEpisode_User>();
                        foreach (
                            SVR_AnimeEpisode_User userRecord in
                            RepoFactory.AnimeEpisode_User.GetByUserIDAndSeriesID(jmmuserID, ser.AnimeSeriesID))
                            dictEpUsers[userRecord.AnimeEpisodeID] = userRecord;

                        foreach (SVR_AnimeEpisode animeep in RepoFactory.AnimeEpisode.GetBySeriesID(ser.AnimeSeriesID))
                        {
                            if (!dictEpUsers.ContainsKey(animeep.AnimeEpisodeID))
                            {
                                epList.Add(animeep);
                                continue;
                            }

                            SVR_AnimeEpisode_User usrRec = dictEpUsers[animeep.AnimeEpisodeID];
                            if (usrRec.WatchedCount == 0 || !usrRec.WatchedDate.HasValue)
                                epList.Add(animeep);
                        }

                        List<AniDB_Episode> aniEpList = RepoFactory.AniDB_Episode.GetByAnimeID(ser.AniDB_ID);
                        Dictionary<int, AniDB_Episode> dictAniEps = new Dictionary<int, AniDB_Episode>();
                        foreach (AniDB_Episode aniep in aniEpList)
                            dictAniEps[aniep.EpisodeID] = aniep;

                        List<CL_AnimeEpisode_User> candidateEps = new List<CL_AnimeEpisode_User>();

                        foreach (SVR_AnimeEpisode ep in epList)
                        {
                            if (dictAniEps.ContainsKey(ep.AniDB_EpisodeID))
                            {
                                AniDB_Episode anidbep = dictAniEps[ep.AniDB_EpisodeID];
                                if (anidbep.EpisodeType == (int) EpisodeType.Episode ||
                                    anidbep.EpisodeType == (int) EpisodeType.Special)
                                {
                                    SVR_AnimeEpisode_User userRecord = null;
                                    if (dictEpUsers.ContainsKey(ep.AnimeEpisodeID))
                                    {
                                        userRecord = dictEpUsers[ep.AnimeEpisodeID];
                                        CL_AnimeEpisode_User epContract = userRecord.Contract;
                                        if (epContract != null)
                                            candidateEps.Add(epContract);
                                    }
                                }
                            }
                        }

                        if (candidateEps.Count > 0)
                        {
                            TvDBSummary tvSummary = new TvDBSummary();
                            tvSummary.Populate(ser.AniDB_ID);

                            // sort by episode type and number to find the next episode

                            // this will generate a lot of queries when the user doesn have files
                            // for these episodes
                            int cnt = 0;
                            foreach (CL_AnimeEpisode_User canEp in candidateEps.OrderBy(a => a.EpisodeType)
                                .ThenBy(a => a.EpisodeNumber))
                            {
                                if (dictAniEps.ContainsKey(canEp.AniDB_EpisodeID))
                                {
                                    AniDB_Episode anidbep = dictAniEps[canEp.AniDB_EpisodeID];

                                    SVR_AnimeEpisode_User userEpRecord = null;
                                    if (dictEpUsers.ContainsKey(canEp.AnimeEpisodeID))
                                        userEpRecord = dictEpUsers[canEp.AnimeEpisodeID];

                                    // now refresh from the database to get file count
                                    SVR_AnimeEpisode epFresh = RepoFactory.AnimeEpisode.GetByID(canEp.AnimeEpisodeID);

                                    int fileCount = epFresh.GetVideoLocals(session).Count;
                                    if (fileCount > 0)
                                    {
                                        Metro_Anime_Episode contract = new Metro_Anime_Episode
                                        {
                                            AnimeEpisodeID = epFresh.AnimeEpisodeID,
                                            LocalFileCount = fileCount
                                        };
                                        if (userEpRecord == null)
                                            contract.IsWatched = false;
                                        else
                                            contract.IsWatched = userEpRecord.WatchedCount > 0;

                                        // anidb
                                        contract.EpisodeNumber = anidbep.EpisodeNumber;
                                        contract.EpisodeName = anidbep.RomajiName;
                                        if (!string.IsNullOrEmpty(anidbep.EnglishName))
                                            contract.EpisodeName = anidbep.EnglishName;

                                        contract.EpisodeType = anidbep.EpisodeType;
                                        contract.LengthSeconds = anidbep.LengthSeconds;
                                        contract.AirDate = anidbep.GetAirDateFormatted();

                                        // tvdb
                                        SetTvDBInfo(tvSummary, anidbep, ref contract);


                                        ret.NextEpisodesToWatch.Add(contract);
                                        cnt++;
                                    }
                                }
                                if (cnt == maxEpisodeRecords) break;
                            }
                        }
                    }

                    return ret;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public Metro_Anime_Summary GetAnimeSummary(int animeID)
        {
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();

                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, animeID);
                    if (anime == null) return null;

                    SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

                    Metro_Anime_Summary summ = new Metro_Anime_Summary
                    {
                        AnimeID = anime.AnimeID,
                        AnimeName = anime.MainTitle,
                        AnimeSeriesID = 0,

                        BeginYear = anime.BeginYear,
                        EndYear = anime.EndYear,
                        PosterName = anime.GetDefaultPosterPathNoBlanks(sessionWrapper)
                    };
                    ImageDetails imgDet = anime.GetDefaultPosterDetailsNoBlanks(sessionWrapper);
                    summ.ImageType = (int) imgDet.ImageType;
                    summ.ImageID = imgDet.ImageID;

                    if (ser != null)
                    {
                        summ.AnimeName = ser.GetSeriesName();
                        summ.AnimeSeriesID = ser.AnimeSeriesID;
                    }

                    return summ;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return null;
        }

        public static void SetTvDBInfo(SVR_AniDB_Anime anime, AniDB_Episode ep, ref Metro_Anime_Episode contract)
        {
            TvDBSummary tvSummary = new TvDBSummary();
            tvSummary.Populate(anime.AnimeID);

            SetTvDBInfo(tvSummary, ep, ref contract);
        }

        public static void SetTvDBInfo(int anidbid, AniDB_Episode ep, ref Metro_Anime_Episode contract)
        {
            TvDBSummary tvSummary = new TvDBSummary();
            tvSummary.Populate(anidbid);

            SetTvDBInfo(tvSummary, ep, ref contract);
        }

        public static void SetTvDBInfo(TvDBSummary tvSummary, AniDB_Episode ep, ref Metro_Anime_Episode contract)
        {
            #region episode override

            // check if this episode has a direct tvdb over-ride
            if (tvSummary.DictTvDBCrossRefEpisodes.ContainsKey(ep.EpisodeID))
            {
                foreach (TvDB_Episode tvep in tvSummary.DictTvDBEpisodes.Values)
                {
                    if (tvSummary.DictTvDBCrossRefEpisodes[ep.EpisodeID] == tvep.Id)
                    {
                        if (string.IsNullOrEmpty(tvep.Overview))
                            contract.EpisodeOverview = "Episode Overview Not Available";
                        else
                            contract.EpisodeOverview = tvep.Overview;

                        if (string.IsNullOrEmpty(tvep.GetFullImagePath()) || !File.Exists(tvep.GetFullImagePath()))
                        {
                            contract.ImageType = 0;
                            contract.ImageID = 0;
                        }
                        else
                        {
                            contract.ImageType = (int) JMMImageType.TvDB_Episode;
                            contract.ImageID = tvep.TvDB_EpisodeID;
                        }

                        if (ServerSettings.EpisodeTitleSource == DataSourceType.TheTvDB &&
                            !string.IsNullOrEmpty(tvep.EpisodeName))
                            contract.EpisodeName = tvep.EpisodeName;

                        return;
                    }
                }
            }

            #endregion

            #region normal episodes

            // now do stuff to improve performance
            if (ep.GetEpisodeTypeEnum() == EpisodeType.Episode)
            {
                if (tvSummary != null && tvSummary.CrossRefTvDBV2 != null && tvSummary.CrossRefTvDBV2.Count > 0)
                {
                    // find the xref that is right
                    // relies on the xref's being sorted by season number and then episode number (desc)
                    List<CrossRef_AniDB_TvDBV2> tvDBCrossRef = tvSummary.CrossRefTvDBV2
                        .OrderByDescending(a => a.AniDBStartEpisodeNumber)
                        .ToList();


                    bool foundStartingPoint = false;
                    CrossRef_AniDB_TvDBV2 xrefBase = null;
                    foreach (CrossRef_AniDB_TvDBV2 xrefTV in tvDBCrossRef)
                    {
                        if (xrefTV.AniDBStartEpisodeType != (int) EpisodeType.Episode) continue;
                        if (ep.EpisodeNumber >= xrefTV.AniDBStartEpisodeNumber)
                        {
                            foundStartingPoint = true;
                            xrefBase = xrefTV;
                            break;
                        }
                    }

                    // we have found the starting epiosde numbder from AniDB
                    // now let's check that the TvDB Season and Episode Number exist
                    if (foundStartingPoint)
                    {
                        Dictionary<int, int> dictTvDBSeasons = null;
                        Dictionary<int, TvDB_Episode> dictTvDBEpisodes = null;
                        foreach (TvDBDetails det in tvSummary.TvDetails.Values)
                        {
                            if (det.TvDBID == xrefBase.TvDBID)
                            {
                                dictTvDBSeasons = det.DictTvDBSeasons;
                                dictTvDBEpisodes = det.DictTvDBEpisodes;
                                break;
                            }
                        }

                        if (dictTvDBSeasons.ContainsKey(xrefBase.TvDBSeasonNumber))
                        {
                            int episodeNumber = dictTvDBSeasons[xrefBase.TvDBSeasonNumber] +
                                                (ep.EpisodeNumber + xrefBase.TvDBStartEpisodeNumber - 2) -
                                                (xrefBase.AniDBStartEpisodeNumber - 1);
                            if (dictTvDBEpisodes.ContainsKey(episodeNumber))
                            {
                                TvDB_Episode tvep = dictTvDBEpisodes[episodeNumber];
                                if (string.IsNullOrEmpty(tvep.Overview))
                                    contract.EpisodeOverview = "Episode Overview Not Available";
                                else
                                    contract.EpisodeOverview = tvep.Overview;

                                if (string.IsNullOrEmpty(tvep.GetFullImagePath()) ||
                                    !File.Exists(tvep.GetFullImagePath()))
                                {
                                    contract.ImageType = 0;
                                    contract.ImageID = 0;
                                }
                                else
                                {
                                    contract.ImageType = (int) JMMImageType.TvDB_Episode;
                                    contract.ImageID = tvep.TvDB_EpisodeID;
                                }

                                if (ServerSettings.EpisodeTitleSource == DataSourceType.TheTvDB &&
                                    !string.IsNullOrEmpty(tvep.EpisodeName))
                                    contract.EpisodeName = tvep.EpisodeName;
                            }
                        }
                    }
                }
            }

            #endregion

            #region special episodes

            if (ep.GetEpisodeTypeEnum() == EpisodeType.Special)
            {
                // find the xref that is right
                // relies on the xref's being sorted by season number and then episode number (desc)

                List<CrossRef_AniDB_TvDBV2> tvDBCrossRef = tvSummary.CrossRefTvDBV2
                    .OrderByDescending(a => a.AniDBStartEpisodeNumber)
                    .ToList();

                bool foundStartingPoint = false;
                CrossRef_AniDB_TvDBV2 xrefBase = null;
                foreach (CrossRef_AniDB_TvDBV2 xrefTV in tvDBCrossRef)
                {
                    if (xrefTV.AniDBStartEpisodeType != (int) EpisodeType.Special) continue;
                    if (ep.EpisodeNumber >= xrefTV.AniDBStartEpisodeNumber)
                    {
                        foundStartingPoint = true;
                        xrefBase = xrefTV;
                        break;
                    }
                }

                if (tvSummary != null && tvSummary.CrossRefTvDBV2 != null && tvSummary.CrossRefTvDBV2.Count > 0)
                {
                    // we have found the starting epiosde numbder from AniDB
                    // now let's check that the TvDB Season and Episode Number exist
                    if (foundStartingPoint)
                    {
                        Dictionary<int, int> dictTvDBSeasons = null;
                        Dictionary<int, TvDB_Episode> dictTvDBEpisodes = null;
                        foreach (TvDBDetails det in tvSummary.TvDetails.Values)
                        {
                            if (det.TvDBID == xrefBase.TvDBID)
                            {
                                dictTvDBSeasons = det.DictTvDBSeasons;
                                dictTvDBEpisodes = det.DictTvDBEpisodes;
                                break;
                            }
                        }

                        if (dictTvDBSeasons.ContainsKey(xrefBase.TvDBSeasonNumber))
                        {
                            int episodeNumber = dictTvDBSeasons[xrefBase.TvDBSeasonNumber] +
                                                (ep.EpisodeNumber + xrefBase.TvDBStartEpisodeNumber - 2) -
                                                (xrefBase.AniDBStartEpisodeNumber - 1);
                            if (dictTvDBEpisodes.ContainsKey(episodeNumber))
                            {
                                TvDB_Episode tvep = dictTvDBEpisodes[episodeNumber];
                                contract.EpisodeOverview = tvep.Overview;

                                if (string.IsNullOrEmpty(tvep.GetFullImagePath()) ||
                                    !File.Exists(tvep.GetFullImagePath()))
                                {
                                    contract.ImageType = 0;
                                    contract.ImageID = 0;
                                }
                                else
                                {
                                    contract.ImageType = (int) JMMImageType.TvDB_Episode;
                                    contract.ImageID = tvep.TvDB_EpisodeID;
                                }

                                if (ServerSettings.EpisodeTitleSource == DataSourceType.TheTvDB &&
                                    !string.IsNullOrEmpty(tvep.EpisodeName))
                                    contract.EpisodeName = tvep.EpisodeName;
                            }
                        }
                    }
                }
            }

            #endregion
        }

        public List<Metro_AniDB_Character> GetCharactersForAnime(int animeID, int maxRecords)
        {
            List<Metro_AniDB_Character> chars = new List<Metro_AniDB_Character>();

            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();

                    List<AniDB_Anime_Character> animeChars =
                        RepoFactory.AniDB_Anime_Character.GetByAnimeID(sessionWrapper, animeID);
                    if (animeChars == null || animeChars.Count == 0) return chars;

                    int cnt = 0;

                    // first get all the main characters
                    foreach (
                        AniDB_Anime_Character animeChar in
                        animeChars.Where(
                            item =>
                                item.CharType.Equals("main character in",
                                    StringComparison.InvariantCultureIgnoreCase)))
                    {
                        cnt++;
                        AniDB_Character chr = RepoFactory.AniDB_Character.GetByCharID(animeChar.CharID);
                        if (chr != null)
                        {
                            Metro_AniDB_Character contract = new Metro_AniDB_Character();
                            chars.Add(chr.ToContractMetro(session, animeChar));
                        }

                        if (cnt == maxRecords) break;
                    }

                    // now get the rest
                    foreach (
                        AniDB_Anime_Character animeChar in
                        animeChars.Where(
                            item =>
                                !item.CharType.Equals("main character in",
                                    StringComparison.InvariantCultureIgnoreCase))
                    )
                    {
                        cnt++;
                        AniDB_Character chr = RepoFactory.AniDB_Character.GetByCharID(animeChar.CharID);
                        if (chr != null)
                        {
                            Metro_AniDB_Character contract = new Metro_AniDB_Character();
                            chars.Add(chr.ToContractMetro(session, animeChar));
                        }

                        if (cnt == maxRecords) break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return chars;
        }

        public List<Metro_Comment> GetTraktCommentsForAnime(int animeID, int maxRecords)
        {
            List<Metro_Comment> comments = new List<Metro_Comment>();

            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    List<TraktV2Comment> commentsTemp = TraktTVHelper.GetShowCommentsV2(session, animeID);

                    if (commentsTemp == null || commentsTemp.Count == 0) return comments;

                    int cnt = 0;
                    foreach (TraktV2Comment sht in commentsTemp)
                    {
                        Metro_Comment comment = new Metro_Comment();

                        Trakt_Friend traktFriend = RepoFactory.Trakt_Friend.GetByUsername(session, sht.user.username);

                        // user details
                        CL_Trakt_User user = new CL_Trakt_User();
                        if (traktFriend == null)
                            comment.UserID = 0;
                        else
                            comment.UserID = traktFriend.Trakt_FriendID;

                        comment.UserName = sht.user.username;

                        // shout details
                        comment.CommentText = sht.comment;
                        comment.IsSpoiler = sht.spoiler;
                        comment.CommentDate = sht.CreatedAtDate;

                        //shout.ImageURL = sht.user.avatar;
                        comment.CommentType = (int) WhatPeopleAreSayingType.TraktComment;
                        comment.Source = "Trakt";

                        cnt++;
                        comments.Add(comment);

                        if (cnt == maxRecords) break;
                    }
                    comments = comments.OrderBy(a => a.CommentDate).ToList();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return comments;
        }

        public List<Metro_Comment> GetAniDBRecommendationsForAnime(int animeID, int maxRecords)
        {
            List<Metro_Comment> contracts = new List<Metro_Comment>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();

                    int cnt = 0;
                    foreach (AniDB_Recommendation rec in RepoFactory.AniDB_Recommendation.GetByAnimeID(sessionWrapper,
                        animeID))
                    {
                        Metro_Comment shout = new Metro_Comment
                        {
                            UserID = rec.UserID,
                            UserName = "",

                            // shout details
                            CommentText = rec.RecommendationText,
                            IsSpoiler = false,
                            CommentDate = null,

                            ImageURL = string.Empty
                        };
                        AniDBRecommendationType recType = (AniDBRecommendationType) rec.RecommendationType;
                        switch (recType)
                        {
                            case AniDBRecommendationType.ForFans:
                                shout.CommentType = (int) WhatPeopleAreSayingType.AniDBForFans;
                                break;
                            case AniDBRecommendationType.MustSee:
                                shout.CommentType = (int) WhatPeopleAreSayingType.AniDBMustSee;
                                break;
                            case AniDBRecommendationType.Recommended:
                                shout.CommentType = (int) WhatPeopleAreSayingType.AniDBRecommendation;
                                break;
                        }

                        shout.Source = "AniDB";

                        cnt++;
                        contracts.Add(shout);

                        if (cnt == maxRecords) break;
                    }

                    return contracts;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return contracts;
            }
        }

        public List<Metro_Anime_Summary> GetSimilarAnimeForAnime(int animeID, int maxRecords, int jmmuserID)
        {
            List<CL_AniDB_Anime_Similar> links = new List<CL_AniDB_Anime_Similar>();
            List<Metro_Anime_Summary> retAnime = new List<Metro_Anime_Summary>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, animeID);
                    if (anime == null) return retAnime;

                    SVR_JMMUser juser = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (juser == null) return retAnime;


                    // first get the related anime
                    foreach (AniDB_Anime_Relation link in anime.GetRelatedAnime())
                    {
                        SVR_AniDB_Anime animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(link.RelatedAnimeID);

                        if (animeLink == null)
                        {
                            // try getting it from anidb now
                            animeLink = ShokoService.AnidbProcessor.GetAnimeInfoHTTP(session, link.RelatedAnimeID,
                                false,
                                false);
                        }

                        if (animeLink == null) continue;
                        if (!juser.AllowedAnime(animeLink)) continue;

                        // check if this anime has a series
                        SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(link.RelatedAnimeID);

                        Metro_Anime_Summary summ = new Metro_Anime_Summary
                        {
                            AnimeID = animeLink.AnimeID,
                            AnimeName = animeLink.MainTitle,
                            AnimeSeriesID = 0,

                            BeginYear = animeLink.BeginYear,
                            EndYear = animeLink.EndYear,
                            //summ.PosterName = animeLink.GetDefaultPosterPathNoBlanks(session);

                            RelationshipType = link.RelationType
                        };
                        ImageDetails imgDet = animeLink.GetDefaultPosterDetailsNoBlanks(sessionWrapper);
                        summ.ImageType = (int) imgDet.ImageType;
                        summ.ImageID = imgDet.ImageID;

                        if (ser != null)
                        {
                            summ.AnimeName = ser.GetSeriesName();
                            summ.AnimeSeriesID = ser.AnimeSeriesID;
                        }

                        retAnime.Add(summ);
                    }

                    // now get similar anime
                    foreach (AniDB_Anime_Similar link in anime.GetSimilarAnime(session))
                    {
                        SVR_AniDB_Anime animeLink =
                            RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, link.SimilarAnimeID);

                        if (animeLink == null)
                        {
                            // try getting it from anidb now
                            animeLink = ShokoService.AnidbProcessor.GetAnimeInfoHTTP(session, link.SimilarAnimeID,
                                false,
                                false);
                        }

                        if (animeLink == null) continue;
                        if (!juser.AllowedAnime(animeLink)) continue;

                        // check if this anime has a series
                        SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(link.SimilarAnimeID);

                        Metro_Anime_Summary summ = new Metro_Anime_Summary
                        {
                            AnimeID = animeLink.AnimeID,
                            AnimeName = animeLink.MainTitle,
                            AnimeSeriesID = 0,

                            BeginYear = animeLink.BeginYear,
                            EndYear = animeLink.EndYear,
                            //summ.PosterName = animeLink.GetDefaultPosterPathNoBlanks(session);

                            RelationshipType = "Recommendation"
                        };
                        ImageDetails imgDet = animeLink.GetDefaultPosterDetailsNoBlanks(sessionWrapper);
                        summ.ImageType = (int) imgDet.ImageType;
                        summ.ImageID = imgDet.ImageID;

                        if (ser != null)
                        {
                            summ.AnimeName = ser.GetSeriesName();
                            summ.AnimeSeriesID = ser.AnimeSeriesID;
                        }

                        retAnime.Add(summ);

                        if (retAnime.Count == maxRecords) break;
                    }

                    return retAnime;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return retAnime;
            }
        }

        public List<CL_VideoDetailed> GetFilesForEpisode(int episodeID, int userID)
        {
            try
            {
                SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(episodeID);
                if (ep != null)
                    return ep.GetVideoDetailedContracts(userID);
                else
                    return new List<CL_VideoDetailed>();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return new List<CL_VideoDetailed>();
        }

        public CL_Response<CL_AnimeEpisode_User> ToggleWatchedStatusOnEpisode(int animeEpisodeID,
            bool watchedStatus, int userID)
        {
            CL_Response<CL_AnimeEpisode_User> response =
                new CL_Response<CL_AnimeEpisode_User>
                {
                    ErrorMessage = "",
                    Result = null
                };
            try
            {
                SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(animeEpisodeID);
                if (ep == null)
                {
                    response.ErrorMessage = "Could not find anime episode record";
                    return response;
                }

                ep.ToggleWatchedStatus(watchedStatus, true, DateTime.Now, false, false, userID, true);
                ep.GetAnimeSeries().UpdateStats(true, false, true);
                //StatsCache.Instance.UpdateUsingSeries(ep.GetAnimeSeries().AnimeSeriesID);

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

        public string UpdateAnimeData(int animeID)
        {
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ShokoService.AnidbProcessor.GetAnimeInfoHTTP(session, animeID, true, false);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return "";
        }
    }
}