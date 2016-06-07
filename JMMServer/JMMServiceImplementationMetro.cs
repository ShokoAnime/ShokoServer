using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AniDBAPI;
using BinaryNorthwest;
using JMMContracts;
using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.Providers.TraktTV;
using JMMServer.Providers.TvDB;
using JMMServer.Repositories;
using NLog;

namespace JMMServer
{
    public class JMMServiceImplementationMetro : IJMMServerMetro
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public Contract_ServerStatus GetServerStatus()
        {
            var contract = new Contract_ServerStatus();

            try
            {
                contract.HashQueueCount = JMMService.CmdProcessorHasher.QueueCount;
                contract.HashQueueState = JMMService.CmdProcessorHasher.QueueState;

                contract.GeneralQueueCount = JMMService.CmdProcessorGeneral.QueueCount;
                contract.GeneralQueueState = JMMService.CmdProcessorGeneral.QueueState;

                contract.ImagesQueueCount = JMMService.CmdProcessorImages.QueueCount;
                contract.ImagesQueueState = JMMService.CmdProcessorImages.QueueState;

                contract.IsBanned = JMMService.AnidbProcessor.IsBanned;
                contract.BanReason = JMMService.AnidbProcessor.BanTime.ToString();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return contract;
        }

        public Contract_ServerSettings GetServerSettings()
        {
            var contract = new Contract_ServerSettings();

            try
            {
                return ServerSettings.ToContract();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return contract;
        }

        public bool PostCommentShow(string traktID, string commentText, bool isSpoiler, ref string returnMessage)
        {
            return TraktTVHelper.PostCommentShow(traktID, commentText, isSpoiler, ref returnMessage);
        }

        public MetroContract_CommunityLinks GetCommunityLinks(int animeID)
        {
            var contract = new MetroContract_CommunityLinks();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repAnime = new AniDB_AnimeRepository();

                    var anime = repAnime.GetByAnimeID(session, animeID);
                    if (anime == null) return null;

                    //AniDB
                    contract.AniDB_ID = animeID;
                    contract.AniDB_URL = string.Format(Constants.URLS.AniDB_Series, animeID);
                    contract.AniDB_DiscussURL = string.Format(Constants.URLS.AniDB_SeriesDiscussion, animeID);

                    // MAL
                    var malRef = anime.GetCrossRefMAL(session);
                    if (malRef != null && malRef.Count > 0)
                    {
                        contract.MAL_ID = malRef[0].MALID.ToString();
                        contract.MAL_URL = string.Format(Constants.URLS.MAL_Series, malRef[0].MALID);
                        //contract.MAL_DiscussURL = string.Format(Constants.URLS.MAL_SeriesDiscussion, malRef[0].MALID, malRef[0].MALTitle);
                        contract.MAL_DiscussURL = string.Format(Constants.URLS.MAL_Series, malRef[0].MALID);
                    }

                    // TvDB
                    var tvdbRef = anime.GetCrossRefTvDBV2(session);
                    if (tvdbRef != null && tvdbRef.Count > 0)
                    {
                        contract.TvDB_ID = tvdbRef[0].TvDBID.ToString();
                        contract.TvDB_URL = string.Format(Constants.URLS.TvDB_Series, tvdbRef[0].TvDBID);
                    }

                    // Trakt
                    var traktRef = anime.GetCrossRefTraktV2(session);
                    if (traktRef != null && traktRef.Count > 0)
                    {
                        contract.Trakt_ID = traktRef[0].TraktID;
                        contract.Trakt_URL = string.Format(Constants.URLS.Trakt_Series, traktRef[0].TraktID);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }

            return contract;
        }

        public Contract_JMMUser AuthenticateUser(string username, string password)
        {
            var repUsers = new JMMUserRepository();

            try
            {
                var user = repUsers.AuthenticateUser(username, password);
                if (user == null) return null;

                return user.ToContract();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public List<Contract_JMMUser> GetAllUsers()
        {
            var repUsers = new JMMUserRepository();

            // get all the users
            var userList = new List<Contract_JMMUser>();

            try
            {
                var users = repUsers.GetAll();
                foreach (var user in users)
                    userList.Add(user.ToContract());
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return userList;
        }

        public List<Contract_AnimeGroup> GetAllGroups(int userID)
        {
            var grps = new List<Contract_AnimeGroup>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repGroups = new AnimeGroupRepository();
                    var repUserGroups = new AnimeGroup_UserRepository();

                    var allGrps = repGroups.GetAll(session);

                    // user records
                    var repGroupUser = new AnimeGroup_UserRepository();
                    var userRecordList = repGroupUser.GetByUserID(session, userID);
                    var dictUserRecords = new Dictionary<int, AnimeGroup_User>();
                    foreach (var grpUser in userRecordList)
                        dictUserRecords[grpUser.AnimeGroupID] = grpUser;

                    foreach (var ag in allGrps)
                    {
                        AnimeGroup_User userRec = null;
                        if (dictUserRecords.ContainsKey(ag.AnimeGroupID))
                            userRec = dictUserRecords[ag.AnimeGroupID];

                        // calculate stats
                        var contract = ag.ToContract(userRec);
                        contract.ServerPosterPath = ag.GetPosterPathNoBlanks(session);
                        grps.Add(contract);
                    }

                    grps.Sort();
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return grps;
        }

        public List<MetroContract_Anime_Summary> GetAnimeWithNewEpisodes(int maxRecords, int jmmuserID)
        {
            var retAnime = new List<MetroContract_Anime_Summary>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repEps = new AnimeEpisodeRepository();
                    var repEpUser = new AnimeEpisode_UserRepository();
                    var repSeries = new AnimeSeriesRepository();
                    var repUsers = new JMMUserRepository();
                    var repVids = new VideoLocalRepository();

                    var user = repUsers.GetByID(session, jmmuserID);
                    if (user == null) return retAnime;

                    var sql = "Select ae.AnimeSeriesID, max(vl.DateTimeCreated) as MaxDate " +
                              "From VideoLocal vl " +
                              "INNER JOIN CrossRef_File_Episode xref ON vl.Hash = xref.Hash " +
                              "INNER JOIN AnimeEpisode ae ON ae.AniDB_EpisodeID = xref.EpisodeID " +
                              "GROUP BY ae.AnimeSeriesID " +
                              "ORDER BY MaxDate desc ";
                    var results = DatabaseHelper.GetData(sql);

                    var numEps = 0;
                    foreach (object[] res in results)
                    {
                        var animeSeriesID = int.Parse(res[0].ToString());

                        var ser = repSeries.GetByID(session, animeSeriesID);
                        if (ser == null) continue;

                        if (!user.AllowedSeries(session, ser)) continue;

                        var serUser = ser.GetUserRecord(session, jmmuserID);

                        var vids = repVids.GetMostRecentlyAddedForAnime(session, 1, ser.AniDB_ID);
                        if (vids.Count == 0) continue;

                        var eps = vids[0].GetAnimeEpisodes(session);
                        if (eps.Count == 0) continue;

                        var epContract = eps[0].ToContract(session, jmmuserID);
                        if (epContract != null)
                        {
                            var anidb_anime = ser.GetAnime(session);

                            var summ = new MetroContract_Anime_Summary();
                            summ.AnimeID = ser.AniDB_ID;
                            summ.AnimeName = ser.GetSeriesName(session);
                            summ.AnimeSeriesID = ser.AnimeSeriesID;
                            summ.BeginYear = anidb_anime.BeginYear;
                            summ.EndYear = anidb_anime.EndYear;
                            //summ.PosterName = anidb_anime.GetDefaultPosterPathNoBlanks(session);
                            if (serUser != null)
                                summ.UnwatchedEpisodeCount = serUser.UnwatchedEpisodeCount;
                            else
                                summ.UnwatchedEpisodeCount = 0;

                            var imgDet = anidb_anime.GetDefaultPosterDetailsNoBlanks(session);
                            summ.ImageType = (int)imgDet.ImageType;
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
                logger.ErrorException(ex.ToString(), ex);
            }

            return retAnime;
        }

        public List<MetroContract_Anime_Summary> GetAnimeContinueWatching(int maxRecords, int jmmuserID)
        {
            var retAnime = new List<MetroContract_Anime_Summary>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repGF = new GroupFilterRepository();

                    var repUsers = new JMMUserRepository();
                    var user = repUsers.GetByID(session, jmmuserID);
                    if (user == null) return retAnime;

                    // find the locked Continue Watching Filter
                    GroupFilter gf = null;
                    var lockedGFs = repGF.GetLockedGroupFilters(session);
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

                    if (gf == null) return retAnime;

                    // Get all the groups 
                    // it is more efficient to just get the full list of groups and then filter them later
                    var repGroups = new AnimeGroupRepository();
                    var allGrps = repGroups.GetAll(session);

                    // get all the user records
                    var repUserRecords = new AnimeGroup_UserRepository();
                    var userRecords = repUserRecords.GetByUserID(session, jmmuserID);
                    var dictUserRecords = new Dictionary<int, AnimeGroup_User>();
                    foreach (var userRec in userRecords)
                        dictUserRecords[userRec.AnimeGroupID] = userRec;

                    // get all the groups in this filter for this user
                    var groups = StatsCache.Instance.StatUserGroupFilter[user.JMMUserID][gf.GroupFilterID];

                    var comboGroups = new List<Contract_AnimeGroup>();
                    foreach (var grp in allGrps)
                    {
                        if (groups.Contains(grp.AnimeGroupID))
                        {
                            AnimeGroup_User userRec = null;
                            if (dictUserRecords.ContainsKey(grp.AnimeGroupID))
                                userRec = dictUserRecords[grp.AnimeGroupID];

                            var rec = grp.ToContract(userRec);
                            comboGroups.Add(rec);
                        }
                    }

                    // apply sorting
                    var sortCriteria = GroupFilterHelper.GetSortDescriptions(gf);
                    comboGroups = Sorting.MultiSort(comboGroups, sortCriteria);

                    if (StatsCache.Instance.StatUserGroupFilter.ContainsKey(user.JMMUserID) &&
                        StatsCache.Instance.StatUserGroupFilter[user.JMMUserID].ContainsKey(gf.GroupFilterID))
                    {
                        var repSeries = new AnimeSeriesRepository();
                        foreach (var grp in comboGroups)
                        {
                            var imp = new JMMServiceImplementation();
                            foreach (var ser in repSeries.GetByGroupID(session, grp.AnimeGroupID))
                            {
                                if (!user.AllowedSeries(ser)) continue;

                                var serUser = ser.GetUserRecord(session, jmmuserID);

                                var ep = imp.GetNextUnwatchedEpisode(session, ser.AnimeSeriesID, jmmuserID);
                                if (ep != null)
                                {
                                    var anidb_anime = ser.GetAnime(session);

                                    var summ = new MetroContract_Anime_Summary();
                                    summ.AnimeID = ser.AniDB_ID;
                                    summ.AnimeName = ser.GetSeriesName(session);
                                    summ.AnimeSeriesID = ser.AnimeSeriesID;
                                    summ.BeginYear = anidb_anime.BeginYear;
                                    summ.EndYear = anidb_anime.EndYear;
                                    //summ.PosterName = anidb_anime.GetDefaultPosterPathNoBlanks(session);

                                    if (serUser != null)
                                        summ.UnwatchedEpisodeCount = serUser.UnwatchedEpisodeCount;
                                    else
                                        summ.UnwatchedEpisodeCount = 0;

                                    var imgDet = anidb_anime.GetDefaultPosterDetailsNoBlanks(session);
                                    summ.ImageType = (int)imgDet.ImageType;
                                    summ.ImageID = imgDet.ImageID;

                                    retAnime.Add(summ);


                                    // Lets only return the specified amount
                                    if (retAnime.Count == maxRecords) return retAnime;
                                }
                                else
                                    logger.Info(
                                        string.Format("GetAnimeContinueWatching:Skipping Anime - no episodes: {0}",
                                            ser.AniDB_ID));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }

            return retAnime;
        }

        public List<MetroContract_Anime_Summary> GetAnimeCalendar(int jmmuserID, int startDateSecs, int endDateSecs,
            int maxRecords)
        {
            var retAnime = new List<MetroContract_Anime_Summary>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repAnime = new AniDB_AnimeRepository();
                    var repUsers = new JMMUserRepository();
                    var repSeries = new AnimeSeriesRepository();


                    var user = repUsers.GetByID(session, jmmuserID);
                    if (user == null) return retAnime;

                    var startDate = Utils.GetAniDBDateAsDate(startDateSecs);
                    var endDate = Utils.GetAniDBDateAsDate(endDateSecs);

                    var animes = repAnime.GetForDate(session, startDate.Value, endDate.Value);
                    foreach (var anidb_anime in animes)
                    {
                        if (!user.AllowedAnime(anidb_anime)) continue;

                        var ser = repSeries.GetByAnimeID(anidb_anime.AnimeID);

                        var summ = new MetroContract_Anime_Summary();

                        summ.AirDateAsSeconds = anidb_anime.AirDateAsSeconds;
                        summ.AnimeID = anidb_anime.AnimeID;
                        if (ser != null)
                        {
                            summ.AnimeName = ser.GetSeriesName(session);
                            summ.AnimeSeriesID = ser.AnimeSeriesID;
                        }
                        else
                        {
                            summ.AnimeName = anidb_anime.MainTitle;
                            summ.AnimeSeriesID = 0;
                        }
                        summ.BeginYear = anidb_anime.BeginYear;
                        summ.EndYear = anidb_anime.EndYear;
                        summ.PosterName = anidb_anime.GetDefaultPosterPathNoBlanks(session);

                        var imgDet = anidb_anime.GetDefaultPosterDetailsNoBlanks(session);
                        summ.ImageType = (int)imgDet.ImageType;
                        summ.ImageID = imgDet.ImageID;

                        retAnime.Add(summ);
                        if (retAnime.Count == maxRecords) break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }

            return retAnime;
        }

        public List<MetroContract_Anime_Summary> SearchAnime(int jmmuserID, string queryText, int maxRecords)
        {
            var retAnime = new List<MetroContract_Anime_Summary>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repAnime = new AniDB_AnimeRepository();
                    var repUsers = new JMMUserRepository();
                    var repSeries = new AnimeSeriesRepository();


                    var user = repUsers.GetByID(session, jmmuserID);
                    if (user == null) return retAnime;


                    var animes = repAnime.SearchByName(session, queryText);
                    foreach (var anidb_anime in animes)
                    {
                        if (!user.AllowedAnime(anidb_anime)) continue;

                        var ser = repSeries.GetByAnimeID(anidb_anime.AnimeID);

                        var summ = new MetroContract_Anime_Summary();

                        summ.AirDateAsSeconds = anidb_anime.AirDateAsSeconds;
                        summ.AnimeID = anidb_anime.AnimeID;
                        if (ser != null)
                        {
                            summ.AnimeName = ser.GetSeriesName(session);
                            summ.AnimeSeriesID = ser.AnimeSeriesID;
                        }
                        else
                        {
                            summ.AnimeName = anidb_anime.MainTitle;
                            summ.AnimeSeriesID = 0;
                        }
                        summ.BeginYear = anidb_anime.BeginYear;
                        summ.EndYear = anidb_anime.EndYear;
                        summ.PosterName = anidb_anime.GetDefaultPosterPathNoBlanks(session);

                        var imgDet = anidb_anime.GetDefaultPosterDetailsNoBlanks(session);
                        summ.ImageType = (int)imgDet.ImageType;
                        summ.ImageID = imgDet.ImageID;

                        retAnime.Add(summ);
                        if (retAnime.Count == maxRecords) break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }

            return retAnime;
        }

        public MetroContract_Anime_Detail GetAnimeDetail(int animeID, int jmmuserID, int maxEpisodeRecords)
        {
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repSeries = new AnimeSeriesRepository();
                    var repAnime = new AniDB_AnimeRepository();

                    var anime = repAnime.GetByAnimeID(session, animeID);
                    if (anime == null) return null;

                    var ser = repSeries.GetByAnimeID(session, animeID);

                    var ret = new MetroContract_Anime_Detail();
                    ret.AnimeID = anime.AnimeID;

                    if (ser != null)
                        ret.AnimeName = ser.GetSeriesName(session);
                    else
                        ret.AnimeName = anime.MainTitle;

                    if (ser != null)
                        ret.AnimeSeriesID = ser.AnimeSeriesID;
                    else
                        ret.AnimeSeriesID = 0;

                    ret.BeginYear = anime.BeginYear;
                    ret.EndYear = anime.EndYear;

                    var imgDet = anime.GetDefaultPosterDetailsNoBlanks(session);
                    ret.PosterImageType = (int)imgDet.ImageType;
                    ret.PosterImageID = imgDet.ImageID;

                    var imgDetFan = anime.GetDefaultFanartDetailsNoBlanks(session);
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
                        var serUserRec = ser.GetUserRecord(session, jmmuserID);
                        if (ser != null)
                            ret.UnwatchedEpisodeCount = serUserRec.UnwatchedEpisodeCount;
                        else
                            ret.UnwatchedEpisodeCount = 0;

                        var repEps = new AnimeEpisodeRepository();
                        var repEpUser = new AnimeEpisode_UserRepository();

                        var epList = new List<AnimeEpisode>();
                        var dictEpUsers = new Dictionary<int, AnimeEpisode_User>();
                        foreach (
                            var userRecord in repEpUser.GetByUserIDAndSeriesID(session, jmmuserID, ser.AnimeSeriesID))
                            dictEpUsers[userRecord.AnimeEpisodeID] = userRecord;

                        foreach (var animeep in repEps.GetBySeriesID(session, ser.AnimeSeriesID))
                        {
                            if (!dictEpUsers.ContainsKey(animeep.AnimeEpisodeID))
                            {
                                epList.Add(animeep);
                                continue;
                            }

                            var usrRec = dictEpUsers[animeep.AnimeEpisodeID];
                            if (usrRec.WatchedCount == 0 || !usrRec.WatchedDate.HasValue)
                                epList.Add(animeep);
                        }

                        var repAniEps = new AniDB_EpisodeRepository();
                        var aniEpList = repAniEps.GetByAnimeID(session, ser.AniDB_ID);
                        var dictAniEps = new Dictionary<int, AniDB_Episode>();
                        foreach (var aniep in aniEpList)
                            dictAniEps[aniep.EpisodeID] = aniep;

                        var candidateEps = new List<Contract_AnimeEpisode>();

                        foreach (var ep in epList)
                        {
                            if (dictAniEps.ContainsKey(ep.AniDB_EpisodeID))
                            {
                                var anidbep = dictAniEps[ep.AniDB_EpisodeID];
                                if (anidbep.EpisodeType == (int)enEpisodeType.Episode ||
                                    anidbep.EpisodeType == (int)enEpisodeType.Special)
                                {
                                    AnimeEpisode_User userRecord = null;
                                    if (dictEpUsers.ContainsKey(ep.AnimeEpisodeID))
                                        userRecord = dictEpUsers[ep.AnimeEpisodeID];

                                    var epContract = ep.ToContract(anidbep, new List<VideoLocal>(), userRecord,
                                        serUserRec);
                                    candidateEps.Add(epContract);
                                }
                            }
                        }

                        if (candidateEps.Count > 0)
                        {
                            var tvSummary = new TvDBSummary();
                            tvSummary.Populate(ser.AniDB_ID);

                            // sort by episode type and number to find the next episode
                            var sortCriteria = new List<SortPropOrFieldAndDirection>();
                            sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeType", false, SortType.eInteger));
                            sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeNumber", false, SortType.eInteger));
                            candidateEps = Sorting.MultiSort(candidateEps, sortCriteria);

                            // this will generate a lot of queries when the user doesn have files
                            // for these episodes
                            var cnt = 0;
                            foreach (var canEp in candidateEps)
                            {
                                if (dictAniEps.ContainsKey(canEp.AniDB_EpisodeID))
                                {
                                    var anidbep = dictAniEps[canEp.AniDB_EpisodeID];

                                    AnimeEpisode_User userEpRecord = null;
                                    if (dictEpUsers.ContainsKey(canEp.AnimeEpisodeID))
                                        userEpRecord = dictEpUsers[canEp.AnimeEpisodeID];

                                    // now refresh from the database to get file count
                                    var epFresh = repEps.GetByID(session, canEp.AnimeEpisodeID);

                                    var fileCount = epFresh.GetVideoLocals(session).Count;
                                    if (fileCount > 0)
                                    {
                                        var contract = new MetroContract_Anime_Episode();
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
                logger.ErrorException(ex.ToString(), ex);
                return null;
            }
        }

        public MetroContract_Anime_Summary GetAnimeSummary(int animeID)
        {
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repEps = new AnimeEpisodeRepository();
                    var repSeries = new AnimeSeriesRepository();
                    var repAnime = new AniDB_AnimeRepository();

                    var anime = repAnime.GetByAnimeID(session, animeID);
                    if (anime == null) return null;

                    var ser = repSeries.GetByAnimeID(session, animeID);

                    var summ = new MetroContract_Anime_Summary();
                    summ.AnimeID = anime.AnimeID;
                    summ.AnimeName = anime.MainTitle;
                    summ.AnimeSeriesID = 0;

                    summ.BeginYear = anime.BeginYear;
                    summ.EndYear = anime.EndYear;
                    summ.PosterName = anime.GetDefaultPosterPathNoBlanks(session);

                    var imgDet = anime.GetDefaultPosterDetailsNoBlanks(session);
                    summ.ImageType = (int)imgDet.ImageType;
                    summ.ImageID = imgDet.ImageID;

                    if (ser != null)
                    {
                        summ.AnimeName = ser.GetSeriesName(session);
                        summ.AnimeSeriesID = ser.AnimeSeriesID;
                    }

                    return summ;
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }

            return null;
        }

        public List<MetroContract_AniDB_Character> GetCharactersForAnime(int animeID, int maxRecords)
        {
            var chars = new List<MetroContract_AniDB_Character>();

            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repAnimeChar = new AniDB_Anime_CharacterRepository();
                    var repChar = new AniDB_CharacterRepository();

                    var animeChars = repAnimeChar.GetByAnimeID(session, animeID);
                    if (animeChars == null || animeChars.Count == 0) return chars;

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
                        var chr = repChar.GetByCharID(animeChar.CharID);
                        if (chr != null)
                        {
                            var contract = new MetroContract_AniDB_Character();
                            chars.Add(chr.ToContractMetro(session, animeChar));
                        }

                        if (cnt == maxRecords) break;
                    }

                    // now get the rest
                    foreach (
                        var animeChar in
                            animeChars.Where(
                                item =>
                                    !item.CharType.Equals("main character in",
                                        StringComparison.InvariantCultureIgnoreCase)))
                    {
                        cnt++;
                        var chr = repChar.GetByCharID(animeChar.CharID);
                        if (chr != null)
                        {
                            var contract = new MetroContract_AniDB_Character();
                            chars.Add(chr.ToContractMetro(session, animeChar));
                        }

                        if (cnt == maxRecords) break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return chars;
        }

        public List<MetroContract_Comment> GetTraktCommentsForAnime(int animeID, int maxRecords)
        {
            var comments = new List<MetroContract_Comment>();

            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repFriends = new Trakt_FriendRepository();

                    var commentsTemp = TraktTVHelper.GetShowCommentsV2(session, animeID);

                    if (commentsTemp == null || commentsTemp.Count == 0) return comments;

                    var cnt = 0;
                    foreach (var sht in commentsTemp)
                    {
                        var comment = new MetroContract_Comment();

                        var traktFriend = repFriends.GetByUsername(session, sht.user.username);

                        // user details
                        var user = new Contract_Trakt_User();
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
                        comment.CommentType = (int)WhatPeopleAreSayingType.TraktComment;
                        comment.Source = "Trakt";

                        cnt++;
                        comments.Add(comment);

                        if (cnt == maxRecords) break;
                    }

                    if (comments.Count > 0)
                    {
                        var sortCriteria = new List<SortPropOrFieldAndDirection>();
                        sortCriteria.Add(new SortPropOrFieldAndDirection("ShoutDate", false, SortType.eDateTime));
                        comments = Sorting.MultiSort(comments, sortCriteria);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return comments;
        }

        public List<MetroContract_Comment> GetAniDBRecommendationsForAnime(int animeID, int maxRecords)
        {
            var contracts = new List<MetroContract_Comment>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repBA = new AniDB_RecommendationRepository();

                    var cnt = 0;
                    foreach (var rec in repBA.GetByAnimeID(session, animeID))
                    {
                        var shout = new MetroContract_Comment();

                        shout.UserID = rec.UserID;
                        shout.UserName = "";

                        // shout details
                        shout.CommentText = rec.RecommendationText;
                        shout.IsSpoiler = false;
                        shout.CommentDate = null;

                        shout.ImageURL = string.Empty;

                        var recType = (AniDBRecommendationType)rec.RecommendationType;
                        switch (recType)
                        {
                            case AniDBRecommendationType.ForFans:
                                shout.CommentType = (int)WhatPeopleAreSayingType.AniDBForFans;
                                break;
                            case AniDBRecommendationType.MustSee:
                                shout.CommentType = (int)WhatPeopleAreSayingType.AniDBMustSee;
                                break;
                            case AniDBRecommendationType.Recommended:
                                shout.CommentType = (int)WhatPeopleAreSayingType.AniDBRecommendation;
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
                logger.ErrorException(ex.ToString(), ex);
                return contracts;
            }
        }

        public List<MetroContract_Anime_Summary> GetSimilarAnimeForAnime(int animeID, int maxRecords, int jmmuserID)
        {
            var links = new List<Contract_AniDB_Anime_Similar>();
            var retAnime = new List<MetroContract_Anime_Summary>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repAnime = new AniDB_AnimeRepository();
                    var anime = repAnime.GetByAnimeID(session, animeID);
                    if (anime == null) return retAnime;

                    var repUsers = new JMMUserRepository();
                    var juser = repUsers.GetByID(session, jmmuserID);
                    if (juser == null) return retAnime;

                    var repSeries = new AnimeSeriesRepository();

                    // first get the related anime
                    foreach (var link in anime.GetRelatedAnime())
                    {
                        var animeLink = repAnime.GetByAnimeID(link.RelatedAnimeID);

                        if (animeLink == null)
                        {
                            // try getting it from anidb now
                            animeLink = JMMService.AnidbProcessor.GetAnimeInfoHTTP(session, link.RelatedAnimeID, false,
                                false);
                        }

                        if (animeLink == null) continue;
                        if (!juser.AllowedAnime(animeLink)) continue;

                        // check if this anime has a series
                        var ser = repSeries.GetByAnimeID(link.RelatedAnimeID);

                        var summ = new MetroContract_Anime_Summary();
                        summ.AnimeID = animeLink.AnimeID;
                        summ.AnimeName = animeLink.MainTitle;
                        summ.AnimeSeriesID = 0;

                        summ.BeginYear = animeLink.BeginYear;
                        summ.EndYear = animeLink.EndYear;
                        //summ.PosterName = animeLink.GetDefaultPosterPathNoBlanks(session);

                        summ.RelationshipType = link.RelationType;

                        var imgDet = animeLink.GetDefaultPosterDetailsNoBlanks(session);
                        summ.ImageType = (int)imgDet.ImageType;
                        summ.ImageID = imgDet.ImageID;

                        if (ser != null)
                        {
                            summ.AnimeName = ser.GetSeriesName(session);
                            summ.AnimeSeriesID = ser.AnimeSeriesID;
                        }

                        retAnime.Add(summ);
                    }

                    // now get similar anime
                    foreach (var link in anime.GetSimilarAnime(session))
                    {
                        var animeLink = repAnime.GetByAnimeID(session, link.SimilarAnimeID);

                        if (animeLink == null)
                        {
                            // try getting it from anidb now
                            animeLink = JMMService.AnidbProcessor.GetAnimeInfoHTTP(session, link.SimilarAnimeID, false,
                                false);
                        }

                        if (animeLink == null) continue;
                        if (!juser.AllowedAnime(animeLink)) continue;

                        // check if this anime has a series
                        var ser = repSeries.GetByAnimeID(session, link.SimilarAnimeID);

                        var summ = new MetroContract_Anime_Summary();
                        summ.AnimeID = animeLink.AnimeID;
                        summ.AnimeName = animeLink.MainTitle;
                        summ.AnimeSeriesID = 0;

                        summ.BeginYear = animeLink.BeginYear;
                        summ.EndYear = animeLink.EndYear;
                        //summ.PosterName = animeLink.GetDefaultPosterPathNoBlanks(session);

                        summ.RelationshipType = "Recommendation";

                        var imgDet = animeLink.GetDefaultPosterDetailsNoBlanks(session);
                        summ.ImageType = (int)imgDet.ImageType;
                        summ.ImageID = imgDet.ImageID;

                        if (ser != null)
                        {
                            summ.AnimeName = ser.GetSeriesName(session);
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
                logger.ErrorException(ex.ToString(), ex);
                return retAnime;
            }
        }

        public List<Contract_VideoDetailed> GetFilesForEpisode(int episodeID, int userID)
        {
            try
            {
                var repEps = new AnimeEpisodeRepository();
                var ep = repEps.GetByID(episodeID);
                if (ep != null)
                    return ep.GetVideoDetailedContracts(userID);
                return new List<Contract_VideoDetailed>();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return new List<Contract_VideoDetailed>();
        }

        public Contract_ToggleWatchedStatusOnEpisode_Response ToggleWatchedStatusOnEpisode(int animeEpisodeID,
            bool watchedStatus, int userID)
        {
            var response = new Contract_ToggleWatchedStatusOnEpisode_Response();
            response.ErrorMessage = "";
            response.AnimeEpisode = null;

            try
            {
                var repEps = new AnimeEpisodeRepository();
                var ep = repEps.GetByID(animeEpisodeID);
                if (ep == null)
                {
                    response.ErrorMessage = "Could not find anime episode record";
                    return response;
                }

                ep.ToggleWatchedStatus(watchedStatus, true, DateTime.Now, false, false, userID, true);
                ep.GetAnimeSeries().UpdateStats(true, false, true);
                //StatsCache.Instance.UpdateUsingSeries(ep.GetAnimeSeries().AnimeSeriesID);

                // refresh from db
                ep = repEps.GetByID(animeEpisodeID);

                response.AnimeEpisode = ep.ToContract(userID);

                return response;
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
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
                logger.ErrorException(ex.ToString(), ex);
            }
            return "";
        }

        public List<Contract_AnimeEpisode> GetEpisodesRecentlyAddedSummary(int maxRecords, int jmmuserID)
        {
            var retEps = new List<Contract_AnimeEpisode>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repEps = new AnimeEpisodeRepository();
                    var repEpUser = new AnimeEpisode_UserRepository();
                    var repSeries = new AnimeSeriesRepository();
                    var repUsers = new JMMUserRepository();
                    var repVids = new VideoLocalRepository();

                    var user = repUsers.GetByID(session, jmmuserID);
                    if (user == null) return retEps;

                    var sql = "Select ae.AnimeSeriesID, max(vl.DateTimeCreated) as MaxDate " +
                              "From VideoLocal vl " +
                              "INNER JOIN CrossRef_File_Episode xref ON vl.Hash = xref.Hash " +
                              "INNER JOIN AnimeEpisode ae ON ae.AniDB_EpisodeID = xref.EpisodeID " +
                              "GROUP BY ae.AnimeSeriesID " +
                              "ORDER BY MaxDate desc ";
                    var results = DatabaseHelper.GetData(sql);

                    var numEps = 0;
                    foreach (object[] res in results)
                    {
                        var animeSeriesID = int.Parse(res[0].ToString());

                        var ser = repSeries.GetByID(session, animeSeriesID);
                        if (ser == null) continue;

                        if (!user.AllowedSeries(session, ser)) continue;


                        var vids = repVids.GetMostRecentlyAddedForAnime(session, 1, ser.AniDB_ID);
                        if (vids.Count == 0) continue;

                        var eps = vids[0].GetAnimeEpisodes(session);
                        if (eps.Count == 0) continue;

                        var epContract = eps[0].ToContract(session, jmmuserID);
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
                logger.ErrorException(ex.ToString(), ex);
            }

            return retEps;
        }

        public List<MetroContract_Anime_Summary> GetAnimeContinueWatching_old(int maxRecords, int jmmuserID)
        {
            var retAnime = new List<MetroContract_Anime_Summary>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repEps = new AnimeEpisodeRepository();
                    var repAnimeSer = new AnimeSeriesRepository();
                    var repSeriesUser = new AnimeSeries_UserRepository();
                    var repUsers = new JMMUserRepository();

                    var start = DateTime.Now;

                    var user = repUsers.GetByID(session, jmmuserID);
                    if (user == null) return retAnime;

                    // get a list of series that is applicable
                    var allSeriesUser = repSeriesUser.GetMostRecentlyWatched(session, jmmuserID);

                    var ts = DateTime.Now - start;
                    logger.Info(string.Format("GetAnimeContinueWatching:Series: {0}", ts.TotalMilliseconds));


                    var imp = new JMMServiceImplementation();
                    foreach (var userRecord in allSeriesUser)
                    {
                        start = DateTime.Now;

                        var series = repAnimeSer.GetByID(session, userRecord.AnimeSeriesID);
                        if (series == null) continue;

                        if (!user.AllowedSeries(session, series))
                        {
                            logger.Info(string.Format("GetAnimeContinueWatching:Skipping Anime - not allowed: {0}",
                                series.AniDB_ID));
                            continue;
                        }

                        var serUser = series.GetUserRecord(session, jmmuserID);

                        var ep = imp.GetNextUnwatchedEpisode(session, userRecord.AnimeSeriesID, jmmuserID);
                        if (ep != null)
                        {
                            var anidb_anime = series.GetAnime(session);

                            var summ = new MetroContract_Anime_Summary();
                            summ.AnimeID = series.AniDB_ID;
                            summ.AnimeName = series.GetSeriesName(session);
                            summ.AnimeSeriesID = series.AnimeSeriesID;
                            summ.BeginYear = anidb_anime.BeginYear;
                            summ.EndYear = anidb_anime.EndYear;
                            //summ.PosterName = anidb_anime.GetDefaultPosterPathNoBlanks(session);

                            if (serUser != null)
                                summ.UnwatchedEpisodeCount = serUser.UnwatchedEpisodeCount;
                            else
                                summ.UnwatchedEpisodeCount = 0;

                            var imgDet = anidb_anime.GetDefaultPosterDetailsNoBlanks(session);
                            summ.ImageType = (int)imgDet.ImageType;
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
                logger.ErrorException(ex.ToString(), ex);
            }

            return retAnime;
        }

        public static void SetTvDBInfo(AniDB_Anime anime, AniDB_Episode ep, ref MetroContract_Anime_Episode contract)
        {
            var tvSummary = new TvDBSummary();
            tvSummary.Populate(anime.AnimeID);

            SetTvDBInfo(tvSummary, ep, ref contract);
        }

        public static void SetTvDBInfo(int anidbid, AniDB_Episode ep, ref MetroContract_Anime_Episode contract)
        {
            var tvSummary = new TvDBSummary();
            tvSummary.Populate(anidbid);

            SetTvDBInfo(tvSummary, ep, ref contract);
        }

        public static void SetTvDBInfo(TvDBSummary tvSummary, AniDB_Episode ep, ref MetroContract_Anime_Episode contract)
        {
            #region episode override

            // check if this episode has a direct tvdb over-ride
            if (tvSummary.DictTvDBCrossRefEpisodes.ContainsKey(ep.EpisodeID))
            {
                foreach (var tvep in tvSummary.DictTvDBEpisodes.Values)
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
                            contract.ImageType = (int)JMMImageType.TvDB_Episode;
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
                    var sortCriteria = new List<SortPropOrFieldAndDirection>();
                    sortCriteria.Add(new SortPropOrFieldAndDirection("AniDBStartEpisodeNumber", true, SortType.eInteger));
                    var tvDBCrossRef = Sorting.MultiSort(tvSummary.CrossRefTvDBV2, sortCriteria);

                    var foundStartingPoint = false;
                    CrossRef_AniDB_TvDBV2 xrefBase = null;
                    foreach (var xrefTV in tvDBCrossRef)
                    {
                        if (xrefTV.AniDBStartEpisodeType != (int)enEpisodeType.Episode) continue;
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
                        foreach (var det in tvSummary.TvDetails.Values)
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
                            var episodeNumber = dictTvDBSeasons[xrefBase.TvDBSeasonNumber] +
                                                (ep.EpisodeNumber + xrefBase.TvDBStartEpisodeNumber - 2) -
                                                (xrefBase.AniDBStartEpisodeNumber - 1);
                            if (dictTvDBEpisodes.ContainsKey(episodeNumber))
                            {
                                var tvep = dictTvDBEpisodes[episodeNumber];
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
                                    contract.ImageType = (int)JMMImageType.TvDB_Episode;
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
                var sortCriteria = new List<SortPropOrFieldAndDirection>();
                sortCriteria.Add(new SortPropOrFieldAndDirection("AniDBStartEpisodeNumber", true, SortType.eInteger));
                var tvDBCrossRef = Sorting.MultiSort(tvSummary.CrossRefTvDBV2, sortCriteria);

                var foundStartingPoint = false;
                CrossRef_AniDB_TvDBV2 xrefBase = null;
                foreach (var xrefTV in tvDBCrossRef)
                {
                    if (xrefTV.AniDBStartEpisodeType != (int)enEpisodeType.Special) continue;
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
                        foreach (var det in tvSummary.TvDetails.Values)
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
                            var episodeNumber = dictTvDBSeasons[xrefBase.TvDBSeasonNumber] +
                                                (ep.EpisodeNumber + xrefBase.TvDBStartEpisodeNumber - 2) -
                                                (xrefBase.AniDBStartEpisodeNumber - 1);
                            if (dictTvDBEpisodes.ContainsKey(episodeNumber))
                            {
                                var tvep = dictTvDBEpisodes[episodeNumber];
                                contract.EpisodeOverview = tvep.Overview;

                                if (string.IsNullOrEmpty(tvep.FullImagePath) || !File.Exists(tvep.FullImagePath))
                                {
                                    contract.ImageType = 0;
                                    contract.ImageID = 0;
                                }
                                else
                                {
                                    contract.ImageType = (int)JMMImageType.TvDB_Episode;
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
    }
}