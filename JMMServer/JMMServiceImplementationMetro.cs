using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AniDBAPI;

using JMMContracts;
using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.ImageDownload;
using JMMServer.Providers.TraktTV;
using JMMServer.Providers.TraktTV.Contracts;
using JMMServer.Providers.TvDB;
using JMMServer.Repositories;
using JMMServer.Repositories.Cached;
using JMMServer.Repositories.Direct;
using JMMServer.Repositories.NHibernate;
using NLog;

namespace JMMServer
{
    public class JMMServiceImplementationMetro : IJMMServerMetro
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public Contract_ServerStatus GetServerStatus()
        {
            Contract_ServerStatus contract = new Contract_ServerStatus();

            try
            {
                contract.HashQueueCount = JMMService.CmdProcessorHasher.QueueCount;
                contract.HashQueueState = JMMService.CmdProcessorHasher.QueueState.formatMessage(); //Deprecated since 3.6.0.0
                contract.HashQueueStateId = (int)JMMService.CmdProcessorHasher.QueueState.queueState;
                contract.HashQueueStateParams = JMMService.CmdProcessorHasher.QueueState.extraParams;

                contract.GeneralQueueCount = JMMService.CmdProcessorGeneral.QueueCount;
                contract.GeneralQueueState = JMMService.CmdProcessorGeneral.QueueState.formatMessage(); //Deprecated since 3.6.0.0
                contract.GeneralQueueStateId = (int)JMMService.CmdProcessorGeneral.QueueState.queueState;
                contract.GeneralQueueStateParams = JMMService.CmdProcessorGeneral.QueueState.extraParams;

                contract.ImagesQueueCount = JMMService.CmdProcessorImages.QueueCount;
                contract.ImagesQueueState = JMMService.CmdProcessorImages.QueueState.formatMessage(); //Deprecated since 3.6.0.0
                contract.ImagesQueueStateId = (int)JMMService.CmdProcessorImages.QueueState.queueState;
                contract.ImagesQueueStateParams = JMMService.CmdProcessorImages.QueueState.extraParams;

                contract.IsBanned = JMMService.AnidbProcessor.IsBanned;
                contract.BanReason = JMMService.AnidbProcessor.BanTime.ToString();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return contract;
        }

        public Contract_ServerSettings GetServerSettings()
        {
            Contract_ServerSettings contract = new Contract_ServerSettings();

            try
            {
                return ServerSettings.ToContract();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return contract;
        }

        public bool PostCommentShow(string traktID, string commentText, bool isSpoiler, ref string returnMessage)
        {
            return TraktTVHelper.PostCommentShow(traktID, commentText, isSpoiler, ref returnMessage);
        }

        public MetroContract_CommunityLinks GetCommunityLinks(int animeID)
        {
            MetroContract_CommunityLinks contract = new MetroContract_CommunityLinks();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();


                    AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, animeID);
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
                    List<CrossRef_AniDB_TvDBV2> tvdbRef = anime.GetCrossRefTvDBV2(sessionWrapper);
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
                logger.Error( ex,ex.ToString());
            }

            return contract;
        }

        public Contract_JMMUser AuthenticateUser(string username, string password)
        {
            try
            {
                return RepoFactory.JMMUser.AuthenticateUser(username, password)?.Contract;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public List<Contract_JMMUser> GetAllUsers()
        {
            // get all the users
            try
            {
                return RepoFactory.JMMUser.GetAll().Select(a => a.Contract).ToList();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return new List<Contract_JMMUser>();
        }

        public List<Contract_AnimeGroup> GetAllGroups(int userID)
        {
            List<Contract_AnimeGroup> grps = new List<Contract_AnimeGroup>();
            try
            {
                foreach (AnimeGroup ag in RepoFactory.AnimeGroup.GetAll())
                {
                    grps.Add(ag.GetUserContract(userID));
                }
                grps.Sort();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return grps;
        }

        public List<Contract_AnimeEpisode> GetEpisodesRecentlyAddedSummary(int maxRecords, int jmmuserID)
        {
            List<Contract_AnimeEpisode> retEps = new List<Contract_AnimeEpisode>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                   
                    JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retEps;

                    string sql = "Select ae.AnimeSeriesID, max(vl.DateTimeCreated) as MaxDate " +
                                 "From VideoLocal vl " +
                                 "INNER JOIN CrossRef_File_Episode xref ON vl.Hash = xref.Hash " +
                                 "INNER JOIN AnimeEpisode ae ON ae.AniDB_EpisodeID = xref.EpisodeID " +
                                 "GROUP BY ae.AnimeSeriesID " +
                                 "ORDER BY MaxDate desc ";
                    ArrayList results = DatabaseExtensions.Instance.GetData(sql);

                    int numEps = 0;
                    foreach (object[] res in results)
                    {
                        int animeSeriesID = int.Parse(res[0].ToString());

                        AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                        if (ser == null) continue;

                        if (!user.AllowedSeries(session, ser)) continue;


                        List<VideoLocal> vids = RepoFactory.VideoLocal.GetMostRecentlyAddedForAnime(1, ser.AniDB_ID);
                        if (vids.Count == 0) continue;

                        List<AnimeEpisode> eps = vids[0].GetAnimeEpisodes();
                        if (eps.Count == 0) continue;

                        Contract_AnimeEpisode epContract = eps[0].GetUserContract(jmmuserID);
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
                logger.Error( ex,ex.ToString());
            }

            return retEps;
        }

        public List<MetroContract_Anime_Summary> GetAnimeWithNewEpisodes(int maxRecords, int jmmuserID)
        {
            List<MetroContract_Anime_Summary> retAnime = new List<MetroContract_Anime_Summary>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();

                    JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retAnime;

                    string sql = "Select ae.AnimeSeriesID, max(vl.DateTimeCreated) as MaxDate " +
                                 "From VideoLocal vl " +
                                 "INNER JOIN CrossRef_File_Episode xref ON vl.Hash = xref.Hash " +
                                 "INNER JOIN AnimeEpisode ae ON ae.AniDB_EpisodeID = xref.EpisodeID " +
                                 "GROUP BY ae.AnimeSeriesID " +
                                 "ORDER BY MaxDate desc ";
                    ArrayList results = DatabaseExtensions.Instance.GetData(sql);

                    int numEps = 0;
                    foreach (object[] res in results)
                    {
                        int animeSeriesID = int.Parse(res[0].ToString());

                        AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                        if (ser == null) continue;

                        if (!user.AllowedSeries(session, ser)) continue;

                        AnimeSeries_User serUser = ser.GetUserRecord(jmmuserID);

                        List<VideoLocal> vids = RepoFactory.VideoLocal.GetMostRecentlyAddedForAnime(1, ser.AniDB_ID);
                        if (vids.Count == 0) continue;

                        List<AnimeEpisode> eps = vids[0].GetAnimeEpisodes();
                        if (eps.Count == 0) continue;

                        Contract_AnimeEpisode epContract = eps[0].GetUserContract(jmmuserID);
                        if (epContract != null)
                        {
                            AniDB_Anime anidb_anime = ser.GetAnime(sessionWrapper);

                            MetroContract_Anime_Summary summ = new MetroContract_Anime_Summary();
                            summ.AnimeID = ser.AniDB_ID;
                            summ.AnimeName = ser.GetSeriesName(sessionWrapper);
                            summ.AnimeSeriesID = ser.AnimeSeriesID;
                            summ.BeginYear = anidb_anime.BeginYear;
                            summ.EndYear = anidb_anime.EndYear;
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
                logger.Error( ex,ex.ToString());
            }

            return retAnime;
        }

        public List<MetroContract_Anime_Summary> GetAnimeContinueWatching_old(int maxRecords, int jmmuserID)
        {
            List<MetroContract_Anime_Summary> retAnime = new List<MetroContract_Anime_Summary>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();

                    DateTime start = DateTime.Now;

                    JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retAnime;

                    // get a list of series that is applicable
                    List<AnimeSeries_User> allSeriesUser = RepoFactory.AnimeSeries_User.GetMostRecentlyWatched(jmmuserID);

                    TimeSpan ts = DateTime.Now - start;
                    logger.Info(string.Format("GetAnimeContinueWatching:Series: {0}", ts.TotalMilliseconds));


                    JMMServiceImplementation imp = new JMMServiceImplementation();
                    foreach (AnimeSeries_User userRecord in allSeriesUser)
                    {
                        start = DateTime.Now;

                        AnimeSeries series = RepoFactory.AnimeSeries.GetByID(userRecord.AnimeSeriesID);
                        if (series == null) continue;

                        if (!user.AllowedSeries(session, series))
                        {
                            logger.Info(string.Format("GetAnimeContinueWatching:Skipping Anime - not allowed: {0}",
                                series.AniDB_ID));
                            continue;
                        }

                        AnimeSeries_User serUser = series.GetUserRecord(jmmuserID);

                        Contract_AnimeEpisode ep = imp.GetNextUnwatchedEpisode(sessionWrapper, userRecord.AnimeSeriesID,
                            jmmuserID);
                        if (ep != null)
                        {
                            AniDB_Anime anidb_anime = series.GetAnime(sessionWrapper);

                            MetroContract_Anime_Summary summ = new MetroContract_Anime_Summary();
                            summ.AnimeID = series.AniDB_ID;
                            summ.AnimeName = series.GetSeriesName(sessionWrapper);
                            summ.AnimeSeriesID = series.AnimeSeriesID;
                            summ.BeginYear = anidb_anime.BeginYear;
                            summ.EndYear = anidb_anime.EndYear;
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
                logger.Error( ex,ex.ToString());
            }

            return retAnime;
        }

        public List<MetroContract_Anime_Summary> GetAnimeContinueWatching(int maxRecords, int jmmuserID)
        {
            List<MetroContract_Anime_Summary> retAnime = new List<MetroContract_Anime_Summary>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();
                    JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retAnime;

                    // find the locked Continue Watching Filter
                    GroupFilter gf = null;
                    List<GroupFilter> lockedGFs = RepoFactory.GroupFilter.GetLockedGroupFilters();
                    if (lockedGFs != null)
                    {
                        // if it already exists we can leave
                        foreach (GroupFilter gfTemp in lockedGFs)
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
                    IEnumerable<Contract_AnimeGroup> comboGroups =
                        gf.GroupsIds[jmmuserID].Select(a => RepoFactory.AnimeGroup.GetByID(a))
                            .Where(a => a != null)
                            .Select(a => a.GetUserContract(jmmuserID));

                    // apply sorting
                    comboGroups= GroupFilterHelper.Sort(comboGroups, gf);
                    
                    foreach (Contract_AnimeGroup grp in comboGroups)
                    {
                        JMMServiceImplementation imp = new JMMServiceImplementation();
                        foreach (AnimeSeries ser in RepoFactory.AnimeSeries.GetByGroupID(grp.AnimeGroupID))
                        {
                            if (!user.AllowedSeries(ser)) continue;

                            AnimeSeries_User serUser = ser.GetUserRecord(jmmuserID);

                            Contract_AnimeEpisode ep = imp.GetNextUnwatchedEpisode(sessionWrapper, ser.AnimeSeriesID, jmmuserID);
                            if (ep != null)
                            {
                                AniDB_Anime anidb_anime = ser.GetAnime(sessionWrapper);

                                MetroContract_Anime_Summary summ = new MetroContract_Anime_Summary();
                                summ.AnimeID = ser.AniDB_ID;
                                summ.AnimeName = ser.GetSeriesName(sessionWrapper);
                                summ.AnimeSeriesID = ser.AnimeSeriesID;
                                summ.BeginYear = anidb_anime.BeginYear;
                                summ.EndYear = anidb_anime.EndYear;
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
                logger.Error( ex,ex.ToString());
            }

            return retAnime;
        }

        public List<MetroContract_Anime_Summary> GetAnimeCalendar(int jmmuserID, int startDateSecs, int endDateSecs,
            int maxRecords)
        {
            List<MetroContract_Anime_Summary> retAnime = new List<MetroContract_Anime_Summary>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();


                    JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retAnime;

                    DateTime? startDate = Utils.GetAniDBDateAsDate(startDateSecs);
                    DateTime? endDate = Utils.GetAniDBDateAsDate(endDateSecs);

                    List<AniDB_Anime> animes = RepoFactory.AniDB_Anime.GetForDate(session, startDate.Value, endDate.Value);
                    foreach (AniDB_Anime anidb_anime in animes)
                    {
                        if (!user.AllowedAnime(anidb_anime)) continue;

                        AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(anidb_anime.AnimeID);

                        MetroContract_Anime_Summary summ = new MetroContract_Anime_Summary();

                        summ.AirDateAsSeconds = anidb_anime.AirDateAsSeconds;
                        summ.AnimeID = anidb_anime.AnimeID;
                        if (ser != null)
                        {
                            summ.AnimeName = ser.GetSeriesName(sessionWrapper);
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
                logger.Error( ex,ex.ToString());
            }

            return retAnime;
        }

        public List<MetroContract_Anime_Summary> SearchAnime(int jmmuserID, string queryText, int maxRecords)
        {
            List<MetroContract_Anime_Summary> retAnime = new List<MetroContract_Anime_Summary>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();
 
   

                    JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retAnime;


                    List<AniDB_Anime> animes = RepoFactory.AniDB_Anime.SearchByName(session, queryText);
                    foreach (AniDB_Anime anidb_anime in animes)
                    {
                        if (!user.AllowedAnime(anidb_anime)) continue;

                        AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(anidb_anime.AnimeID);

                        MetroContract_Anime_Summary summ = new MetroContract_Anime_Summary();

                        summ.AirDateAsSeconds = anidb_anime.AirDateAsSeconds;
                        summ.AnimeID = anidb_anime.AnimeID;
                        if (ser != null)
                        {
                            summ.AnimeName = ser.GetSeriesName(sessionWrapper);
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
                logger.Error( ex,ex.ToString());
            }

            return retAnime;
        }

        public MetroContract_Anime_Detail GetAnimeDetail(int animeID, int jmmuserID, int maxEpisodeRecords)
        {
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();
      

                    AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, animeID);
                    if (anime == null) return null;

                    AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

                    MetroContract_Anime_Detail ret = new MetroContract_Anime_Detail();
                    ret.AnimeID = anime.AnimeID;

                    if (ser != null)
                        ret.AnimeName = ser.GetSeriesName(sessionWrapper);
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

                    ret.AnimeType = anime.AnimeTypeDescription;
                    ret.Description = anime.Description;
                    ret.EpisodeCountNormal = anime.EpisodeCountNormal;
                    ret.EpisodeCountSpecial = anime.EpisodeCountSpecial;


                    ret.AirDate = anime.AirDate;
                    ret.EndDate = anime.EndDate;

                    ret.OverallRating = anime.AniDBRating;
                    ret.TotalVotes = anime.AniDBTotalVotes;
                    ret.AllTags = anime.TagsString;

                    ret.NextEpisodesToWatch = new List<MetroContract_Anime_Episode>();
                    if (ser != null)
                    {
                        AnimeSeries_User serUserRec = ser.GetUserRecord(jmmuserID);
                        if (ser != null)
                            ret.UnwatchedEpisodeCount = serUserRec.UnwatchedEpisodeCount;
                        else
                            ret.UnwatchedEpisodeCount = 0;


                        List<AnimeEpisode> epList = new List<AnimeEpisode>();
                        Dictionary<int, AnimeEpisode_User> dictEpUsers = new Dictionary<int, AnimeEpisode_User>();
                        foreach (
                            AnimeEpisode_User userRecord in
                                RepoFactory.AnimeEpisode_User.GetByUserIDAndSeriesID(jmmuserID, ser.AnimeSeriesID))
                            dictEpUsers[userRecord.AnimeEpisodeID] = userRecord;

                        foreach (AnimeEpisode animeep in RepoFactory.AnimeEpisode.GetBySeriesID(ser.AnimeSeriesID))
                        {
                            if (!dictEpUsers.ContainsKey(animeep.AnimeEpisodeID))
                            {
                                epList.Add(animeep);
                                continue;
                            }

                            AnimeEpisode_User usrRec = dictEpUsers[animeep.AnimeEpisodeID];
                            if (usrRec.WatchedCount == 0 || !usrRec.WatchedDate.HasValue)
                                epList.Add(animeep);
                        }

                        List<AniDB_Episode> aniEpList = RepoFactory.AniDB_Episode.GetByAnimeID(ser.AniDB_ID);
                        Dictionary<int, AniDB_Episode> dictAniEps = new Dictionary<int, AniDB_Episode>();
                        foreach (AniDB_Episode aniep in aniEpList)
                            dictAniEps[aniep.EpisodeID] = aniep;

                        List<Contract_AnimeEpisode> candidateEps = new List<Contract_AnimeEpisode>();

                        foreach (AnimeEpisode ep in epList)
                        {
                            if (dictAniEps.ContainsKey(ep.AniDB_EpisodeID))
                            {
                                AniDB_Episode anidbep = dictAniEps[ep.AniDB_EpisodeID];
                                if (anidbep.EpisodeType == (int) enEpisodeType.Episode ||
                                    anidbep.EpisodeType == (int) enEpisodeType.Special)
                                {
                                    AnimeEpisode_User userRecord = null;
                                    if (dictEpUsers.ContainsKey(ep.AnimeEpisodeID))
                                    {
                                        userRecord = dictEpUsers[ep.AnimeEpisodeID];
                                        Contract_AnimeEpisode epContract = userRecord.Contract;
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
                            foreach (Contract_AnimeEpisode canEp in candidateEps.OrderBy(a=>a.EpisodeType).ThenBy(a=>a.EpisodeNumber))
                            {
                                if (dictAniEps.ContainsKey(canEp.AniDB_EpisodeID))
                                {
                                    AniDB_Episode anidbep = dictAniEps[canEp.AniDB_EpisodeID];

                                    AnimeEpisode_User userEpRecord = null;
                                    if (dictEpUsers.ContainsKey(canEp.AnimeEpisodeID))
                                        userEpRecord = dictEpUsers[canEp.AnimeEpisodeID];

                                    // now refresh from the database to get file count
                                    AnimeEpisode epFresh = RepoFactory.AnimeEpisode.GetByID(canEp.AnimeEpisodeID);

                                    int fileCount = epFresh.GetVideoLocals(session).Count;
                                    if (fileCount > 0)
                                    {
                                        MetroContract_Anime_Episode contract = new MetroContract_Anime_Episode();
                                        contract.AnimeEpisodeID = epFresh.AnimeEpisodeID;
                                        contract.LocalFileCount = fileCount;

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
                                        contract.AirDate = anidbep.AirDateFormatted;

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
                logger.Error( ex,ex.ToString());
                return null;
            }
        }

        public MetroContract_Anime_Summary GetAnimeSummary(int animeID)
        {
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();

                    AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, animeID);
                    if (anime == null) return null;

                    AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

                    MetroContract_Anime_Summary summ = new MetroContract_Anime_Summary();
                    summ.AnimeID = anime.AnimeID;
                    summ.AnimeName = anime.MainTitle;
                    summ.AnimeSeriesID = 0;

                    summ.BeginYear = anime.BeginYear;
                    summ.EndYear = anime.EndYear;
                    summ.PosterName = anime.GetDefaultPosterPathNoBlanks(sessionWrapper);

                    ImageDetails imgDet = anime.GetDefaultPosterDetailsNoBlanks(sessionWrapper);
                    summ.ImageType = (int) imgDet.ImageType;
                    summ.ImageID = imgDet.ImageID;

                    if (ser != null)
                    {
                        summ.AnimeName = ser.GetSeriesName(sessionWrapper);
                        summ.AnimeSeriesID = ser.AnimeSeriesID;
                    }

                    return summ;
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }

            return null;
        }

        public static void SetTvDBInfo(AniDB_Anime anime, AniDB_Episode ep, ref MetroContract_Anime_Episode contract)
        {
            TvDBSummary tvSummary = new TvDBSummary();
            tvSummary.Populate(anime.AnimeID);

            SetTvDBInfo(tvSummary, ep, ref contract);
        }

        public static void SetTvDBInfo(int anidbid, AniDB_Episode ep, ref MetroContract_Anime_Episode contract)
        {
            TvDBSummary tvSummary = new TvDBSummary();
            tvSummary.Populate(anidbid);

            SetTvDBInfo(tvSummary, ep, ref contract);
        }

        public static void SetTvDBInfo(TvDBSummary tvSummary, AniDB_Episode ep, ref MetroContract_Anime_Episode contract)
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

                        if (string.IsNullOrEmpty(tvep.FullImagePath) || !File.Exists(tvep.FullImagePath))
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
            if (ep.EpisodeTypeEnum == enEpisodeType.Episode)
            {
                if (tvSummary != null && tvSummary.CrossRefTvDBV2 != null && tvSummary.CrossRefTvDBV2.Count > 0)
                {
                    // find the xref that is right
                    // relies on the xref's being sorted by season number and then episode number (desc)
                    List<CrossRef_AniDB_TvDBV2> tvDBCrossRef = tvSummary.CrossRefTvDBV2.OrderByDescending(a=>a.AniDBStartEpisodeNumber).ToList();


                    bool foundStartingPoint = false;
                    CrossRef_AniDB_TvDBV2 xrefBase = null;
                    foreach (CrossRef_AniDB_TvDBV2 xrefTV in tvDBCrossRef)
                    {
                        if (xrefTV.AniDBStartEpisodeType != (int) enEpisodeType.Episode) continue;
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

                                if (string.IsNullOrEmpty(tvep.FullImagePath) || !File.Exists(tvep.FullImagePath))
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

            if (ep.EpisodeTypeEnum == enEpisodeType.Special)
            {
                // find the xref that is right
                // relies on the xref's being sorted by season number and then episode number (desc)

                List<CrossRef_AniDB_TvDBV2> tvDBCrossRef = tvSummary.CrossRefTvDBV2.OrderByDescending(a=>a.AniDBStartEpisodeNumber).ToList();

                bool foundStartingPoint = false;
                CrossRef_AniDB_TvDBV2 xrefBase = null;
                foreach (CrossRef_AniDB_TvDBV2 xrefTV in tvDBCrossRef)
                {
                    if (xrefTV.AniDBStartEpisodeType != (int) enEpisodeType.Special) continue;
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

                                if (string.IsNullOrEmpty(tvep.FullImagePath) || !File.Exists(tvep.FullImagePath))
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

        public List<MetroContract_AniDB_Character> GetCharactersForAnime(int animeID, int maxRecords)
        {
            List<MetroContract_AniDB_Character> chars = new List<MetroContract_AniDB_Character>();

            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();
                    
                    List<AniDB_Anime_Character> animeChars = RepoFactory.AniDB_Anime_Character.GetByAnimeID(sessionWrapper, animeID);
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
                            MetroContract_AniDB_Character contract = new MetroContract_AniDB_Character();
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
                            MetroContract_AniDB_Character contract = new MetroContract_AniDB_Character();
                            chars.Add(chr.ToContractMetro(session, animeChar));
                        }

                        if (cnt == maxRecords) break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return chars;
        }

        public List<MetroContract_Comment> GetTraktCommentsForAnime(int animeID, int maxRecords)
        {
            List<MetroContract_Comment> comments = new List<MetroContract_Comment>();

            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    
                    List<TraktV2Comment> commentsTemp = TraktTVHelper.GetShowCommentsV2(session, animeID);

                    if (commentsTemp == null || commentsTemp.Count == 0) return comments;

                    int cnt = 0;
                    foreach (TraktV2Comment sht in commentsTemp)
                    {
                        MetroContract_Comment comment = new MetroContract_Comment();

                        Trakt_Friend traktFriend = RepoFactory.Trakt_Friend.GetByUsername(session, sht.user.username);

                        // user details
                        Contract_Trakt_User user = new Contract_Trakt_User();
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
                logger.Error( ex,ex.ToString());
            }
            return comments;
        }

        public List<MetroContract_Comment> GetAniDBRecommendationsForAnime(int animeID, int maxRecords)
        {
            List<MetroContract_Comment> contracts = new List<MetroContract_Comment>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();

                    int cnt = 0;
                    foreach (AniDB_Recommendation rec in RepoFactory.AniDB_Recommendation.GetByAnimeID(sessionWrapper, animeID))
                    {
                        MetroContract_Comment shout = new MetroContract_Comment();

                        shout.UserID = rec.UserID;
                        shout.UserName = "";

                        // shout details
                        shout.CommentText = rec.RecommendationText;
                        shout.IsSpoiler = false;
                        shout.CommentDate = null;

                        shout.ImageURL = string.Empty;

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
                logger.Error( ex,ex.ToString());
                return contracts;
            }
        }

        public List<MetroContract_Anime_Summary> GetSimilarAnimeForAnime(int animeID, int maxRecords, int jmmuserID)
        {
            List<Contract_AniDB_Anime_Similar> links = new List<Contract_AniDB_Anime_Similar>();
            List<MetroContract_Anime_Summary> retAnime = new List<MetroContract_Anime_Summary>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();
                    AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, animeID);
                    if (anime == null) return retAnime;

                    JMMUser juser = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (juser == null) return retAnime;


                    // first get the related anime
                    foreach (AniDB_Anime_Relation link in anime.GetRelatedAnime())
                    {
                        AniDB_Anime animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(link.RelatedAnimeID);

                        if (animeLink == null)
                        {
                            // try getting it from anidb now
                            animeLink = JMMService.AnidbProcessor.GetAnimeInfoHTTP(session, link.RelatedAnimeID, false,
                                false);
                        }

                        if (animeLink == null) continue;
                        if (!juser.AllowedAnime(animeLink)) continue;

                        // check if this anime has a series
                        AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(link.RelatedAnimeID);

                        MetroContract_Anime_Summary summ = new MetroContract_Anime_Summary();
                        summ.AnimeID = animeLink.AnimeID;
                        summ.AnimeName = animeLink.MainTitle;
                        summ.AnimeSeriesID = 0;

                        summ.BeginYear = animeLink.BeginYear;
                        summ.EndYear = animeLink.EndYear;
                        //summ.PosterName = animeLink.GetDefaultPosterPathNoBlanks(session);

                        summ.RelationshipType = link.RelationType;

                        ImageDetails imgDet = animeLink.GetDefaultPosterDetailsNoBlanks(sessionWrapper);
                        summ.ImageType = (int) imgDet.ImageType;
                        summ.ImageID = imgDet.ImageID;

                        if (ser != null)
                        {
                            summ.AnimeName = ser.GetSeriesName(sessionWrapper);
                            summ.AnimeSeriesID = ser.AnimeSeriesID;
                        }

                        retAnime.Add(summ);
                    }

                    // now get similar anime
                    foreach (AniDB_Anime_Similar link in anime.GetSimilarAnime(session))
                    {
                        AniDB_Anime animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(sessionWrapper, link.SimilarAnimeID);

                        if (animeLink == null)
                        {
                            // try getting it from anidb now
                            animeLink = JMMService.AnidbProcessor.GetAnimeInfoHTTP(session, link.SimilarAnimeID, false,
                                false);
                        }

                        if (animeLink == null) continue;
                        if (!juser.AllowedAnime(animeLink)) continue;

                        // check if this anime has a series
                        AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(link.SimilarAnimeID);

                        MetroContract_Anime_Summary summ = new MetroContract_Anime_Summary();
                        summ.AnimeID = animeLink.AnimeID;
                        summ.AnimeName = animeLink.MainTitle;
                        summ.AnimeSeriesID = 0;

                        summ.BeginYear = animeLink.BeginYear;
                        summ.EndYear = animeLink.EndYear;
                        //summ.PosterName = animeLink.GetDefaultPosterPathNoBlanks(session);

                        summ.RelationshipType = "Recommendation";

                        ImageDetails imgDet = animeLink.GetDefaultPosterDetailsNoBlanks(sessionWrapper);
                        summ.ImageType = (int) imgDet.ImageType;
                        summ.ImageID = imgDet.ImageID;

                        if (ser != null)
                        {
                            summ.AnimeName = ser.GetSeriesName(sessionWrapper);
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
                logger.Error( ex,ex.ToString());
                return retAnime;
            }
        }

        public List<Contract_VideoDetailed> GetFilesForEpisode(int episodeID, int userID)
        {
            try
            {
                AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(episodeID);
                if (ep != null)
                    return ep.GetVideoDetailedContracts(userID);
                else
                    return new List<Contract_VideoDetailed>();
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return new List<Contract_VideoDetailed>();
        }

        public Contract_ToggleWatchedStatusOnEpisode_Response ToggleWatchedStatusOnEpisode(int animeEpisodeID,
            bool watchedStatus, int userID)
        {
            Contract_ToggleWatchedStatusOnEpisode_Response response =
                new Contract_ToggleWatchedStatusOnEpisode_Response();
            response.ErrorMessage = "";
            response.AnimeEpisode = null;

            try
            {
                AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(animeEpisodeID);
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

                response.AnimeEpisode = ep.GetUserContract(userID);

                return response;
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
                response.ErrorMessage = ex.Message;
                return response;
            }
        }

        public string UpdateAnimeData(int animeID)
        {
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    JMMService.AnidbProcessor.GetAnimeInfoHTTP(session, animeID, true, false);
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return "";
        }
    }
}