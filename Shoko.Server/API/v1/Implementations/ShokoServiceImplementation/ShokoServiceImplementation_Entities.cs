using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using AniDBAPI;
using AniDBAPI.Commands;
using Shoko.Models;
using Shoko.Models.Azure;
using Shoko.Models.Server;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Client;
using Shoko.Models.Interfaces;
using NLog;
using NutzCode.CloudFileSystem;
using Pri.LongPath;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Commands.MAL;
using Shoko.Server.Commands.TvDB;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.MyAnimeList;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Models.TvDB;
using Shoko.Server.Commands.Plex;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Providers.TraktTV.Contracts;
using Shoko.Server.Tasks;

namespace Shoko.Server
{
    public partial class ShokoServiceImplementation : IShokoServer
    {


        #region Episodes and Files

        /// <summary>
        /// Finds the previous episode for use int the next unwatched episode
        /// </summary>
        /// <param name="animeSeriesID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        public CL_AnimeEpisode_User GetPreviousEpisodeForUnwatched(int animeSeriesID, int userID)
        {
            try
            {
                CL_AnimeEpisode_User nextEp = GetNextUnwatchedEpisode(animeSeriesID, userID);
                if (nextEp == null) return null;

                int epType = nextEp.EpisodeType;
                int epNum = nextEp.EpisodeNumber - 1;

                if (epNum <= 0) return null;

                SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (series == null) return null;

                List<AniDB_Episode> anieps = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeTypeNumber(series.AniDB_ID,
                    (EpisodeType) epType,
                    epNum);
                if (anieps.Count == 0) return null;

                SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(anieps[0].EpisodeID);
                return ep?.GetUserContract(userID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public CL_AnimeEpisode_User GetNextUnwatchedEpisode(int animeSeriesID, int userID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetNextUnwatchedEpisode(session.Wrap(), animeSeriesID, userID);
            }
        }

        public CL_AnimeEpisode_User GetNextUnwatchedEpisode(ISessionWrapper session, int animeSeriesID, int userID)
        {
            try
            {
                // get all the data first
                // we do this to reduce the amount of database calls, which makes it a lot faster
                SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (series == null) return null;

                //List<AnimeEpisode> epList = repEps.GetUnwatchedEpisodes(animeSeriesID, userID);
                List<AnimeEpisode> epList = new List<AnimeEpisode>();
                Dictionary<int, SVR_AnimeEpisode_User> dictEpUsers = new Dictionary<int, SVR_AnimeEpisode_User>();
                foreach (
                    SVR_AnimeEpisode_User userRecord in RepoFactory.AnimeEpisode_User.GetByUserIDAndSeriesID(userID,
                        animeSeriesID))
                    dictEpUsers[userRecord.AnimeEpisodeID] = userRecord;

                foreach (AnimeEpisode animeep in RepoFactory.AnimeEpisode.GetBySeriesID(animeSeriesID))
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

                List<AniDB_Episode> aniEpList = RepoFactory.AniDB_Episode.GetByAnimeID(series.AniDB_ID);
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
                                userRecord = dictEpUsers[ep.AnimeEpisodeID];

                            CL_AnimeEpisode_User epContract = ep.GetUserContract(userID);
                            if (epContract != null)
                                candidateEps.Add(epContract);
                        }
                    }
                }

                if (candidateEps.Count == 0) return null;


                // this will generate a lot of queries when the user doesn have files
                // for these episodes
                foreach (CL_AnimeEpisode_User canEp in candidateEps.OrderBy(a => a.EpisodeType)
                    .ThenBy(a => a.EpisodeNumber))
                {
                    // now refresh from the database to get file count
                    SVR_AnimeEpisode epFresh = RepoFactory.AnimeEpisode.GetByID(canEp.AnimeEpisodeID);
                    if (epFresh.GetVideoLocals().Count > 0)
                        return epFresh.GetUserContract(userID);
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public List<CL_AnimeEpisode_User> GetAllUnwatchedEpisodes(int animeSeriesID, int userID)
        {
            List<CL_AnimeEpisode_User> ret = new List<CL_AnimeEpisode_User>();

            try
            {
                return
                    RepoFactory.AnimeEpisode.GetBySeriesID(animeSeriesID)
                        .Select(a => a.GetUserContract(userID))
                        .Where(a => a != null)
                        .Where(a => a.WatchedCount == 0)
                        .OrderBy(a => a.EpisodeType)
                        .ThenBy(a => a.EpisodeNumber)
                        .ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ret;
            }
        }

        public CL_AnimeEpisode_User GetNextUnwatchedEpisodeForGroup(int animeGroupID, int userID)
        {
            try
            {
                SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return null;

                List<SVR_AnimeSeries> allSeries = grp.GetAllSeries().OrderBy(a => a.AirDate).ToList();


                foreach (SVR_AnimeSeries ser in allSeries)
                {
                    CL_AnimeEpisode_User contract = GetNextUnwatchedEpisode(ser.AnimeSeriesID, userID);
                    if (contract != null) return contract;
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public List<CL_AnimeEpisode_User> GetContinueWatchingFilter(int userID, int maxRecords)
        {
            List<CL_AnimeEpisode_User> retEps = new List<CL_AnimeEpisode_User>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();
                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                    if (user == null) return retEps;

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

                    if ((gf == null) || !gf.GroupsIds.ContainsKey(userID))
                        return retEps;
                    IEnumerable<CL_AnimeGroup_User> comboGroups =
                        gf.GroupsIds[userID]
                            .Select(a => RepoFactory.AnimeGroup.GetByID(a))
                            .Where(a => a != null)
                            .Select(a => a.GetUserContract(userID));


                    // apply sorting
                    comboGroups = GroupFilterHelper.Sort(comboGroups, gf);


                    foreach (CL_AnimeGroup_User grp in comboGroups)
                    {
                        List<SVR_AnimeSeries> sers = RepoFactory.AnimeSeries.GetByGroupID(grp.AnimeGroupID)
                            .OrderBy(a => a.AirDate)
                            .ToList();

                        List<int> seriesWatching = new List<int>();

                        foreach (SVR_AnimeSeries ser in sers)
                        {
                            if (!user.AllowedSeries(ser)) continue;
                            bool useSeries = true;

                            if (seriesWatching.Count > 0)
                            {
                                if (ser.GetAnime().AnimeType == (int) AnimeType.TVSeries)
                                {
                                    // make sure this series is not a sequel to an existing series we have already added
                                    foreach (AniDB_Anime_Relation rel in ser.GetAnime().GetRelatedAnime())
                                    {
                                        if (rel.RelationType.ToLower().Trim().Equals("sequel") ||
                                            rel.RelationType.ToLower().Trim().Equals("prequel"))
                                            useSeries = false;
                                    }
                                }
                            }

                            if (!useSeries) continue;


                            CL_AnimeEpisode_User ep =
                                GetNextUnwatchedEpisode(sessionWrapper, ser.AnimeSeriesID, userID);
                            if (ep != null)
                            {
                                retEps.Add(ep);

                                // Lets only return the specified amount
                                if (retEps.Count == maxRecords)
                                    return retEps;

                                if (ser.GetAnime().AnimeType == (int) AnimeType.TVSeries)
                                    seriesWatching.Add(ser.AniDB_ID);
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

        /// <summary>
        /// Gets a list of episodes watched based on the most recently watched series
        /// It will return the next episode to watch in the most recent 10 series
        /// </summary>
        /// <returns></returns>
        public List<CL_AnimeEpisode_User> GetEpisodesToWatch_RecentlyWatched(int maxRecords, int jmmuserID)
        {
            List<CL_AnimeEpisode_User> retEps = new List<CL_AnimeEpisode_User>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();

                    DateTime start = DateTime.Now;

                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retEps;

                    // get a list of series that is applicable
                    List<SVR_AnimeSeries_User> allSeriesUser =
                        RepoFactory.AnimeSeries_User.GetMostRecentlyWatched(jmmuserID);

                    TimeSpan ts = DateTime.Now - start;
                    logger.Info(string.Format("GetEpisodesToWatch_RecentlyWatched:Series: {0}", ts.TotalMilliseconds));
                    start = DateTime.Now;

                    foreach (SVR_AnimeSeries_User userRecord in allSeriesUser)
                    {
                        SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByID(userRecord.AnimeSeriesID);
                        if (series == null) continue;

                        if (!user.AllowedSeries(series)) continue;

                        CL_AnimeEpisode_User ep =
                            GetNextUnwatchedEpisode(sessionWrapper, userRecord.AnimeSeriesID, jmmuserID);
                        if (ep != null)
                        {
                            retEps.Add(ep);

                            // Lets only return the specified amount
                            if (retEps.Count == maxRecords)
                            {
                                ts = DateTime.Now - start;
                                logger.Info(string.Format("GetEpisodesToWatch_RecentlyWatched:Episodes: {0}",
                                    ts.TotalMilliseconds));
                                return retEps;
                            }
                        }
                    }
                    ts = DateTime.Now - start;
                    logger.Info(string.Format("GetEpisodesToWatch_RecentlyWatched:Episodes: {0}",
                        ts.TotalMilliseconds));
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return retEps;
        }

        public List<CL_AnimeEpisode_User> GetEpisodesRecentlyWatched(int maxRecords, int jmmuserID)
        {
            List<CL_AnimeEpisode_User> retEps = new List<CL_AnimeEpisode_User>();
            try
            {
                return
                    RepoFactory.AnimeEpisode_User.GetMostRecentlyWatched(jmmuserID, maxRecords)
                        .Select(a => RepoFactory.AnimeEpisode.GetByID(a.AnimeEpisodeID).GetUserContract(jmmuserID))
                        .ToList();
                /*
                                using (var session = JMMService.SessionFactory.OpenSession())
                                {
                                    AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
                                    JMMUserRepository repUsers = new JMMUserRepository();

                                    JMMUser user = repUsers.GetByID(session, jmmuserID);
                                    if (user == null) return retEps;

                                    // get a list of series that is applicable
                                    List<AnimeEpisode_User> allEpUserRecs = repEpUser.GetMostRecentlyWatched(session, jmmuserID);
                                    foreach (AnimeEpisode_User userRecord in allEpUserRecs)
                                    {
                                        AnimeEpisode ep = repEps.GetByID(session, userRecord.AnimeEpisodeID);
                                        if (ep == null) continue;

                                        Contract_AnimeEpisode epContract = ep.ToContract(session, jmmuserID);
                                        if (epContract != null)
                                        {
                                            retEps.Add(epContract);

                                            // Lets only return the specified amount
                                            if (retEps.Count == maxRecords) return retEps;
                                        }
                                    }
                                }*/
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return retEps;
        }

        public IReadOnlyList<SVR_VideoLocal> GetAllFiles()
        {
            try
            {
                return RepoFactory.VideoLocal.GetAll();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<SVR_VideoLocal>();
            }
        }

        public SVR_VideoLocal GetFileByID(int id)
        {
            try
            {
                return RepoFactory.VideoLocal.GetByID(id);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new SVR_VideoLocal();
            }
        }

        public List<SVR_VideoLocal> GetFilesRecentlyAdded(int max_records)
        {
            try
            {
                return RepoFactory.VideoLocal.GetMostRecentlyAdded(max_records);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<SVR_VideoLocal>();
            }
        }

        public List<CL_AnimeEpisode_User> GetEpisodesRecentlyAdded(int maxRecords, int jmmuserID)
        {
            List<CL_AnimeEpisode_User> retEps = new List<CL_AnimeEpisode_User>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();


                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retEps;

                    // We will deal with a large list, don't perform ops on the whole thing!
                    List<SVR_VideoLocal> vids = RepoFactory.VideoLocal.GetMostRecentlyAdded(-1);
                    int numEps = 0;
                    foreach (SVR_VideoLocal vid in vids)
                    {
                        if (string.IsNullOrEmpty(vid.Hash)) continue;

                        foreach (SVR_AnimeEpisode ep in vid.GetAnimeEpisodes())
                        {
                            if (user.AllowedSeries(ep.GetAnimeSeries()))
                            {
                                CL_AnimeEpisode_User epContract = ep.GetUserContract(jmmuserID);
                                if (epContract != null)
                                {
                                    retEps.Add(epContract);
                                    numEps++;

                                    // Lets only return the specified amount
                                    if (retEps.Count >= maxRecords) return retEps;
                                }
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

        public List<CL_AnimeEpisode_User> GetEpisodesRecentlyAddedSummary(int maxRecords, int jmmuserID)
        {
            List<CL_AnimeEpisode_User> retEps = new List<CL_AnimeEpisode_User>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retEps;

                    DateTime start = DateTime.Now;

                    string sql = "Select ae.AnimeSeriesID, max(vl.DateTimeCreated) as MaxDate " +
                                 "From VideoLocal vl " +
                                 "INNER JOIN CrossRef_File_Episode xref ON vl.Hash = xref.Hash " +
                                 "INNER JOIN AnimeEpisode ae ON ae.AniDB_EpisodeID = xref.EpisodeID " +
                                 "GROUP BY ae.AnimeSeriesID " +
                                 "ORDER BY MaxDate desc ";
                    ArrayList results = DatabaseFactory.Instance.GetData(sql);

                    TimeSpan ts2 = DateTime.Now - start;
                    logger.Info("GetEpisodesRecentlyAddedSummary:RawData in {0} ms", ts2.TotalMilliseconds);
                    start = DateTime.Now;

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
                            if (retEps.Count == maxRecords)
                            {
                                ts2 = DateTime.Now - start;
                                logger.Info("GetEpisodesRecentlyAddedSummary:Episodes in {0} ms",
                                    ts2.TotalMilliseconds);
                                start = DateTime.Now;
                                return retEps;
                            }
                        }
                    }
                    ts2 = DateTime.Now - start;
                    logger.Info("GetEpisodesRecentlyAddedSummary:Episodes in {0} ms", ts2.TotalMilliseconds);
                    start = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return retEps;
        }

        public List<CL_AnimeSeries_User> GetSeriesRecentlyAdded(int maxRecords, int jmmuserID)
        {
            List<CL_AnimeSeries_User> retSeries = new List<CL_AnimeSeries_User>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                    if (user == null) return retSeries;

                    List<SVR_AnimeSeries> series = RepoFactory.AnimeSeries.GetMostRecentlyAdded(maxRecords);
                    int numSeries = 0;
                    foreach (SVR_AnimeSeries ser in series)
                    {
                        if (user.AllowedSeries(ser))
                        {
                            CL_AnimeSeries_User serContract = ser.GetUserContract(jmmuserID);
                            if (serContract != null)
                            {
                                retSeries.Add(serContract);
                                numSeries++;

                                // Lets only return the specified amount
                                if (retSeries.Count == maxRecords) return retSeries;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return retSeries;
        }

        public CL_AnimeEpisode_User GetLastWatchedEpisodeForSeries(int animeSeriesID, int jmmuserID)
        {
            try
            {
                return RepoFactory.AnimeEpisode_User.GetLastWatchedEpisodeForSeries(animeSeriesID, jmmuserID)?.Contract;
                /*
                                using (var session = JMMService.SessionFactory.OpenSession())
                                {
                                    AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
                                    JMMUserRepository repUsers = new JMMUserRepository();

                                    JMMUser user = repUsers.GetByID(session, jmmuserID);
                                    if (user == null) return null;

                                    List<AnimeEpisode_User> userRecords = repEpUser.GetLastWatchedEpisodeForSeries(session, animeSeriesID, jmmuserID);
                                    if (userRecords == null || userRecords.Count == 0) return null;

                                    AnimeEpisode ep = repEps.GetByID(session, userRecords[0].AnimeEpisodeID);
                                    if (ep == null) return null;

                                    return ep.ToContract(session, jmmuserID);
                                }*/
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return null;
        }

        public CL_AnimeEpisode_User GetEpisode(int animeEpisodeID, int userID)
        {
            try
            {
                return RepoFactory.AnimeEpisode.GetByID(animeEpisodeID)?.GetUserContract(userID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public IReadOnlyList<AnimeEpisode> GetAllEpisodes()
        {
            try
            {
                return RepoFactory.AnimeEpisode.GetAll();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public CL_AnimeEpisode_User GetEpisodeByAniDBEpisodeID(int episodeID, int userID)
        {
            try
            {
                return RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(episodeID)?.GetUserContract(userID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public string RemoveAssociationOnFile(int videoLocalID, int aniDBEpisodeID)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video record";
                if (string.IsNullOrEmpty(vid.Hash)) //this shouldn't happen
                    return "Could not desassociate a cloud file without hash, hash it locally first";

                int? animeSeriesID = null;
                foreach (AnimeEpisode ep in vid.GetAnimeEpisodes())
                {
                    if (ep.AniDB_EpisodeID != aniDBEpisodeID) continue;

                    animeSeriesID = ep.AnimeSeriesID;
                    CrossRef_File_Episode xref =
                        RepoFactory.CrossRef_File_Episode.GetByHashAndEpisodeID(vid.Hash, ep.AniDB_EpisodeID);
                    if (xref != null)
                    {
                        if (xref.CrossRefSource == (int) CrossRefSource.AniDB)
                            return "Cannot remove associations created from AniDB data";

                        // delete cross ref from web cache
                        CommandRequest_WebCacheDeleteXRefFileEpisode cr =
                            new CommandRequest_WebCacheDeleteXRefFileEpisode(vid.Hash,
                                ep.AniDB_EpisodeID);
                        cr.Save();

                        RepoFactory.CrossRef_File_Episode.Delete(xref.CrossRef_File_EpisodeID);
                    }
                }

                if (animeSeriesID.HasValue)
                {
                    SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID.Value);
                    if (ser != null)
                        ser.QueueUpdateStats();
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string SetIgnoreStatusOnFile(int videoLocalID, bool isIgnored)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video record";
                vid.IsIgnored = isIgnored ? 1 : 0;
                RepoFactory.VideoLocal.Save(vid, false);
                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string SetVariationStatusOnFile(int videoLocalID, bool isVariation)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video record";
                vid.IsVariation = isVariation ? 1 : 0;
                RepoFactory.VideoLocal.Save(vid, false);
                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        private void RemoveXRefsForFile(int VideoLocalID)
        {
            SVR_VideoLocal vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
            List<CrossRef_File_Episode> fileEps = RepoFactory.CrossRef_File_Episode.GetByHash(vlocal.Hash);

            foreach (CrossRef_File_Episode fileEp in fileEps)
                RepoFactory.CrossRef_File_Episode.Delete(fileEp.CrossRef_File_EpisodeID);

        }

        public string AssociateSingleFile(int videoLocalID, int animeEpisodeID)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video record";
                if (string.IsNullOrEmpty(vid.Hash))
                    return "Could not associate a cloud file without hash, hash it locally first";

                SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(animeEpisodeID);
                if (ep == null)
                    return "Could not find episode record";

                RemoveXRefsForFile(videoLocalID);
                var com = new CommandRequest_LinkFileManually(videoLocalID, animeEpisodeID);
                com.Save();
                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return string.Empty;
        }

        public string AssociateSingleFileWithMultipleEpisodes(int videoLocalID, int animeSeriesID, int startEpNum,
            int endEpNum)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video record";
                if (vid.Hash == null)
                    return "Could not associate a cloud file without hash, hash it locally first";
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (ser == null)
                    return "Could not find anime series record";
                for (int i = startEpNum; i <= endEpNum; i++)
                {
                    AniDB_Episode aniep = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeNumber(ser.AniDB_ID, i)[0];
                    if (aniep == null)
                        return "Could not find the AniDB episode record";

                    SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(aniep.EpisodeID);
                    if (ep == null)
                        return "Could not find episode record";

                    RemoveXRefsForFile(videoLocalID);
                    var com = new CommandRequest_LinkFileManually(videoLocalID, ep.AnimeEpisodeID);
                    com.Save();
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return string.Empty;
        }

        public string AssociateMultipleFiles(List<int> videoLocalIDs, int animeSeriesID, int startingEpisodeNumber,
            bool singleEpisode)
        {
            try
            {
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (ser == null)
                    return "Could not find anime series record";

                int epNumber = startingEpisodeNumber;
                int total = startingEpisodeNumber + videoLocalIDs.Count - 1;
                int count = 1;

                foreach (int videoLocalID in videoLocalIDs)
                {
                    SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                    if (vid == null)
                        return "Could not find video local record";
                    if (vid.Hash == null)
                        return "Could not associate a cloud file without hash, hash it locally first";

                    List<AniDB_Episode> anieps =
                        RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeNumber(ser.AniDB_ID, epNumber);
                    if (anieps.Count == 0)
                        return "Could not find the AniDB episode record";

                    AniDB_Episode aniep = anieps[0];

                    SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(aniep.EpisodeID);
                    if (ep == null)
                        return "Could not find episode record";

                    RemoveXRefsForFile(videoLocalID);
                    var com = new CommandRequest_LinkFileManually(videoLocalID, ep.AnimeEpisodeID);
                    if (singleEpisode)
                    {
                        com.Percentage = (int) Math.Round((double) count / total * 100);
                    }
                    com.Save();

                    count++;
                    if (!singleEpisode) epNumber++;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return string.Empty;
        }

        public int UpdateAniDBFileData(bool missingInfo, bool outOfDate, bool countOnly)
        {
            return Importer.UpdateAniDBFileData(missingInfo, outOfDate, countOnly);
        }

        public string UpdateFileData(int videoLocalID)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null) return "File could not be found";
                CommandRequest_GetFile cmd = new CommandRequest_GetFile(vid.VideoLocalID, true);
                cmd.Save();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
            return string.Empty;
        }

        public string UpdateEpisodeData(int episodeID)
        {
            try
            {
                CommandRequest_GetEpisode cmd = new CommandRequest_GetEpisode(episodeID);
                cmd.Save();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
            return string.Empty;
        }

        public string RescanFile(int videoLocalID)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null) return "File could not be found";
                if (string.IsNullOrEmpty(vid.Hash))
                    return "Could not Update a cloud file without hash, hash it locally first";
                CommandRequest_ProcessFile cmd = new CommandRequest_ProcessFile(vid.VideoLocalID, true);
                cmd.Save();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.Message);
                return ex.Message;
            }
            return string.Empty;
        }

        public void RehashFile(int videoLocalID)
        {
            SVR_VideoLocal vl = RepoFactory.VideoLocal.GetByID(videoLocalID);

            if (vl != null)
            {
                SVR_VideoLocal_Place pl = vl.GetBestVideoLocalPlace(true);
                if (pl == null)
                {
                    logger.Error("Unable to hash videolocal with id = {videoLocalID}, it has no assigned place");
                    return;
                }
                CommandRequest_HashFile cr_hashfile = new CommandRequest_HashFile(pl.FullServerPath, true);
                cr_hashfile.Save();
            }
        }

        /// <summary>
        /// Delets the VideoLocal record and the associated physical file
        /// </summary>
        /// <param name="videolocalplaceid"></param>
        /// <returns></returns>
        public string DeleteVideoLocalPlaceAndFile(int videolocalplaceid)
        {
            try
            {
                SVR_VideoLocal_Place place = RepoFactory.VideoLocalPlace.GetByID(videolocalplaceid);
                if (place?.VideoLocal == null)
                    return "Database entry does not exist";

                return place.RemoveAndDeleteFile().Item2;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string SetResumePosition(int videoLocalID, long resumeposition, int userID)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video local record";
                vid.SetResumePosition(resumeposition, userID);
                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public List<CL_VideoLocal> GetVideoLocalsForAnime(int animeID, int userID)
        {
            try
            {
                return RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID)
                    .DistinctBy(a => a.Places.FirstOrDefault()?.FullServerPath)
                    .Select(a => a.ToClient(userID))
                    .ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                try
                {
                    // Two checks because the Where doesn't guarantee that First will not be null, only that a not-null value exists
                    return RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID).Where(a =>
                            a.Places.FirstOrDefault(b => !string.IsNullOrEmpty(b.FullServerPath)) != null)
                        .DistinctBy(a => a.Places.FirstOrDefault(b => !string.IsNullOrEmpty(b.FullServerPath))?.FullServerPath)
                        .Select(a => a.ToClient(userID))
                        .ToList();
                }
                catch
                {
                    // Ignore
                }
            }
            return new List<CL_VideoLocal>();
        }

        public AniDB_Vote GetUserVote(int animeID)
        {
            try
            {
                return RepoFactory.AniDB_Vote.GetByEntity(animeID).FirstOrDefault();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return null;
        }

        public void IncrementEpisodeStats(int animeEpisodeID, int userID, int statCountType)
        {
            try
            {
                SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(animeEpisodeID);
                if (ep == null) return;

                SVR_AnimeEpisode_User epUserRecord = ep.GetUserRecord(userID);

                if (epUserRecord == null)
                {
                    epUserRecord = new SVR_AnimeEpisode_User
                    {
                        PlayedCount = 0,
                        StoppedCount = 0,
                        WatchedCount = 0
                    };
                }
                epUserRecord.AnimeEpisodeID = ep.AnimeEpisodeID;
                epUserRecord.AnimeSeriesID = ep.AnimeSeriesID;
                epUserRecord.JMMUserID = userID;
                //epUserRecord.WatchedDate = DateTime.Now;

                switch ((StatCountType) statCountType)
                {
                    case StatCountType.Played:
                        epUserRecord.PlayedCount++;
                        break;
                    case StatCountType.Stopped:
                        epUserRecord.StoppedCount++;
                        break;
                    case StatCountType.Watched:
                        epUserRecord.WatchedCount++;
                        break;
                }

                RepoFactory.AnimeEpisode_User.Save(epUserRecord);

                SVR_AnimeSeries ser = ep.GetAnimeSeries();
                if (ser == null) return;

                SVR_AnimeSeries_User userRecord = ser.GetUserRecord(userID);
                if (userRecord == null)
                    userRecord = new SVR_AnimeSeries_User(userID, ser.AnimeSeriesID);

                switch ((StatCountType) statCountType)
                {
                    case StatCountType.Played:
                        userRecord.PlayedCount++;
                        break;
                    case StatCountType.Stopped:
                        userRecord.StoppedCount++;
                        break;
                    case StatCountType.Watched:
                        userRecord.WatchedCount++;
                        break;
                }

                RepoFactory.AnimeSeries_User.Save(userRecord);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public void DeleteFileFromMyList(int fileID)
        {
            CommandRequest_DeleteFileFromMyList cmd = new CommandRequest_DeleteFileFromMyList(fileID);
            cmd.Save();
        }

        public void ForceAddFileToMyList(string hash)
        {
            try
            {
                CommandRequest_AddFileToMyList cmdAddFile = new CommandRequest_AddFileToMyList(hash);
                cmdAddFile.Save();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public List<AniDB_Episode> GetAniDBEpisodesForAnime(int animeID)
        {
            try
            {
                return RepoFactory.AniDB_Episode.GetByAnimeID(animeID)
                    .OrderBy(a => a.EpisodeType)
                    .ThenBy(a => a.EpisodeNumber)
                    .Cast<AniDB_Episode>()
                    .ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return new List<AniDB_Episode>();
        }

        public List<CL_AnimeEpisode_User> GetEpisodesForSeries(int animeSeriesID, int userID)
        {
            List<CL_AnimeEpisode_User> eps = new List<CL_AnimeEpisode_User>();
            try
            {
                return
                    RepoFactory.AnimeEpisode.GetBySeriesID(animeSeriesID)
                        .Select(a => a.GetUserContract(userID))
                        .Where(a => a != null)
                        .ToList();
                /*
                                DateTime start = DateTime.Now;
                                AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
                                AnimeSeriesRepository repAnimeSer = new AnimeSeriesRepository();
                                VideoLocalRepository repVids = new VideoLocalRepository();
                                CrossRef_File_EpisodeRepository repCrossRefs = new CrossRef_File_EpisodeRepository();

                                // get all the data first
                                // we do this to reduce the amount of database calls, which makes it a lot faster
                                AnimeSeries series = repAnimeSer.GetByID(animeSeriesID);
                                if (series == null) return eps;

                                List<AnimeEpisode> epList = repEps.GetBySeriesID(animeSeriesID);
                                List<AnimeEpisode_User> userRecordList = repEpUsers.GetByUserIDAndSeriesID(userID, animeSeriesID);
                                Dictionary<int, AnimeEpisode_User> dictUserRecords = new Dictionary<int, AnimeEpisode_User>();
                                foreach (AnimeEpisode_User epuser in userRecordList)
                                    dictUserRecords[epuser.AnimeEpisodeID] = epuser;

                                AniDB_EpisodeRepository repAniEps = new AniDB_EpisodeRepository();
                                List<AniDB_Episode> aniEpList = repAniEps.GetByAnimeID(series.AniDB_ID);
                                Dictionary<int, AniDB_Episode> dictAniEps = new Dictionary<int, AniDB_Episode>();
                                foreach (AniDB_Episode aniep in aniEpList)
                                    dictAniEps[aniep.EpisodeID] = aniep;

                                // get all the video local records and cross refs
                                List<VideoLocal> vids = repVids.GetByAniDBAnimeID(series.AniDB_ID);
                                List<CrossRef_File_Episode> crossRefs = repCrossRefs.GetByAnimeID(series.AniDB_ID);

                                TimeSpan ts = DateTime.Now - start;
                                logger.Info("GetEpisodesForSeries: {0} (Database) in {1} ms", series.GetAnime().MainTitle, ts.TotalMilliseconds);


                                start = DateTime.Now;
                                foreach (AnimeEpisode ep in epList)
                                {
                                    if (dictAniEps.ContainsKey(ep.AniDB_EpisodeID))
                                    {
                                        List<VideoLocal> epVids = new List<VideoLocal>();
                                        foreach (CrossRef_File_Episode xref in crossRefs)
                                        {
                                            if (xref.EpisodeID == dictAniEps[ep.AniDB_EpisodeID].EpisodeID)
                                            {
                                                // don't add the same file twice, this will occur when
                                                // one file appears over more than one episodes
                                                Dictionary<string, string> addedFiles = new Dictionary<string, string>();
                                                foreach (VideoLocal vl in vids)
                                                {
                                                    if (string.Equals(xref.Hash, vl.Hash, StringComparison.InvariantCultureIgnoreCase))
                                                    {
                                                        if (!addedFiles.ContainsKey(xref.Hash.Trim().ToUpper()))
                                                        {
                                                            addedFiles[xref.Hash.Trim().ToUpper()] = xref.Hash.Trim().ToUpper();
                                                            epVids.Add(vl);
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        AnimeEpisode_User epuser = null;
                                        if (dictUserRecords.ContainsKey(ep.AnimeEpisodeID))
                                            epuser = dictUserRecords[ep.AnimeEpisodeID];

                                        eps.Add(ep.ToContract(dictAniEps[ep.AniDB_EpisodeID], epVids, epuser, null));
                                    }
                                }

                                ts = DateTime.Now - start;
                                logger.Info("GetEpisodesForSeries: {0} (Contracts) in {1} ms", series.GetAnime().MainTitle, ts.TotalMilliseconds);
                                */
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return eps;
        }

        public List<CL_AnimeEpisode_User> GetEpisodesForSeriesOld(int animeSeriesID)
        {
            List<CL_AnimeEpisode_User> eps = new List<CL_AnimeEpisode_User>();
            try
            {
                SVR_JMMUser user = RepoFactory.JMMUser.GetByID(1) ??
                                   RepoFactory.JMMUser.GetAll().FirstOrDefault(a => a.Username == "Default");
                //HACK (We should have a default user locked)
                if (user != null)
                    return GetEpisodesForSeries(animeSeriesID, user.JMMUserID);
                /*
                                JMMUser u

                                DateTime start = DateTime.Now;
                                AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
                                AnimeSeriesRepository repAnimeSer = new AnimeSeriesRepository();
                                CrossRef_File_EpisodeRepository repCrossRefs = new CrossRef_File_EpisodeRepository();


                                // get all the data first
                                // we do this to reduce the amount of database calls, which makes it a lot faster
                                AnimeSeries series = repAnimeSer.GetByID(animeSeriesID);
                                if (series == null) return eps;

                                List<AnimeEpisode> epList = repEps.GetBySeriesID(animeSeriesID);

                                AniDB_EpisodeRepository repAniEps = new AniDB_EpisodeRepository();
                                List<AniDB_Episode> aniEpList = repAniEps.GetByAnimeID(series.AniDB_ID);
                                Dictionary<int, AniDB_Episode> dictAniEps = new Dictionary<int, AniDB_Episode>();
                                foreach (AniDB_Episode aniep in aniEpList)
                                    dictAniEps[aniep.EpisodeID] = aniep;

                                List<CrossRef_File_Episode> crossRefList = repCrossRefs.GetByAnimeID(series.AniDB_ID);




                                TimeSpan ts = DateTime.Now - start;
                                logger.Info("GetEpisodesForSeries: {0} (Database) in {1} ms", series.GetAnime().MainTitle, ts.TotalMilliseconds);


                                start = DateTime.Now;
                                foreach (AnimeEpisode ep in epList)
                                {
                                    List<CrossRef_File_Episode> xrefs = new List<CrossRef_File_Episode>();
                                    foreach (CrossRef_File_Episode xref in crossRefList)
                                    {
                                        if (ep.AniDB_EpisodeID == xref.EpisodeID)
                                            xrefs.Add(xref);
                                    }

                                    if (dictAniEps.ContainsKey(ep.AniDB_EpisodeID))
                                        eps.Add(ep.ToContractOld(dictAniEps[ep.AniDB_EpisodeID]));
                                }

                                ts = DateTime.Now - start;
                                logger.Info("GetEpisodesForSeries: {0} (Contracts) in {1} ms", series.GetAnime().MainTitle, ts.TotalMilliseconds);
                                */
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return eps;
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

        public List<CL_VideoLocal> GetVideoLocalsForEpisode(int episodeID, int userID)
        {
            List<CL_VideoLocal> contracts = new List<CL_VideoLocal>();
            try
            {
                SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(episodeID);
                if (ep != null)
                {
                    foreach (SVR_VideoLocal vid in ep.GetVideoLocals())
                    {
                        contracts.Add(vid.ToClient(userID));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return contracts;
        }

        public string ToggleWatchedStatusOnVideo(int videoLocalID, bool watchedStatus, int userID)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return "Could not find video local record";
                vid.ToggleWatchedStatus(watchedStatus, true, DateTime.Now, true, userID, true, true);
                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public CL_Response<CL_AnimeEpisode_User> ToggleWatchedStatusOnEpisode(int animeEpisodeID,
            bool watchedStatus, int userID)
        {
            CL_Response<CL_AnimeEpisode_User> response =
                new CL_Response<CL_AnimeEpisode_User>
                {
                    ErrorMessage = string.Empty,
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

                ep.ToggleWatchedStatus(watchedStatus, true, DateTime.Now, false, userID, true);
                ep.GetAnimeSeries().UpdateStats(true, false, true);
                //StatsCache.Instance.UpdateUsingSeries(ep.GetAnimeSeries().AnimeSeriesID);

                // refresh from db


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

        public CL_VideoDetailed GetVideoDetailed(int videoLocalID, int userID)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return null;

                return vid.ToClientDetailed(userID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public List<CL_AnimeEpisode_User> GetEpisodesForFile(int videoLocalID, int userID)
        {
            List<CL_AnimeEpisode_User> contracts = new List<CL_AnimeEpisode_User>();
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                    return contracts;

                foreach (SVR_AnimeEpisode ep in vid.GetAnimeEpisodes())
                {
                    CL_AnimeEpisode_User eps = ep.GetUserContract(userID);
                    if (eps != null)
                        contracts.Add(eps);
                }

                return contracts;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return contracts;
            }
        }

        /// <summary>
        /// Get all the release groups for an episode for which the user is collecting
        /// </summary>
        /// <param name="aniDBEpisodeID"></param>
        /// <returns></returns>
        public List<CL_AniDB_GroupStatus> GetMyReleaseGroupsForAniDBEpisode(int aniDBEpisodeID)
        {
            DateTime start = DateTime.Now;

            List<CL_AniDB_GroupStatus> relGroups = new List<CL_AniDB_GroupStatus>();

            try
            {
                AniDB_Episode aniEp = RepoFactory.AniDB_Episode.GetByEpisodeID(aniDBEpisodeID);
                if (aniEp == null) return relGroups;
                if (aniEp.GetEpisodeTypeEnum() != EpisodeType.Episode) return relGroups;

                SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(aniEp.AnimeID);
                if (series == null) return relGroups;

                // get a list of all the release groups the user is collecting
                Dictionary<int, int> userReleaseGroups = new Dictionary<int, int>();
                foreach (SVR_AnimeEpisode ep in series.GetAnimeEpisodes())
                {
                    List<SVR_VideoLocal> vids = ep.GetVideoLocals();
                    List<string> hashes = vids.Select(a => a.Hash).Distinct().ToList();
                    foreach (string s in hashes)
                    {
                        SVR_VideoLocal vid = vids.First(a => a.Hash == s);
                        AniDB_File anifile = vid.GetAniDBFile();
                        if (anifile != null)
                        {
                            if (!userReleaseGroups.ContainsKey(anifile.GroupID))
                                userReleaseGroups[anifile.GroupID] = 0;

                            userReleaseGroups[anifile.GroupID] = userReleaseGroups[anifile.GroupID] + 1;
                        }
                    }
                }

                // get all the release groups for this series
                List<AniDB_GroupStatus> grpStatuses = RepoFactory.AniDB_GroupStatus.GetByAnimeID(aniEp.AnimeID);
                foreach (AniDB_GroupStatus gs in grpStatuses)
                {
                    if (userReleaseGroups.ContainsKey(gs.GroupID))
                    {
                        if (gs.HasGroupReleasedEpisode(aniEp.EpisodeNumber))
                        {
                            CL_AniDB_GroupStatus cl = gs.ToClient();
                            cl.UserCollecting = true;
                            cl.FileCount = userReleaseGroups[gs.GroupID];
                            relGroups.Add(cl);
                        }
                    }
                }
                TimeSpan ts = DateTime.Now - start;
                logger.Info("GetMyReleaseGroupsForAniDBEpisode  in {0} ms", ts.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return relGroups;
        }

        #endregion

        #region Groups and Series

        public CL_AnimeSeries_User GetSeries(int animeSeriesID, int userID)
        {
            try
            {
                return RepoFactory.AnimeSeries.GetByID(animeSeriesID)?.GetUserContract(userID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return null;
        }

        public List<CL_AnimeSeries_User> GetSeriesByFolderID(int FolderID, int userID, int max)
        {
            try
            {
                int limit = 0;
                List<CL_AnimeSeries_User> list = new List<CL_AnimeSeries_User>();

                foreach (SVR_VideoLocal vi in RepoFactory.VideoLocal.GetByImportFolder(FolderID))
                {
                    foreach (CL_AnimeEpisode_User ae in GetEpisodesForFile(vi.VideoLocalID, userID))
                    {
                        CL_AnimeSeries_User ase = GetSeries(ae.AnimeSeriesID, userID);
                        if (!list.Contains(ase))
                        {
                            limit++;
                            list.Add(ase);
                            if (limit >= max)
                            {
                                break;
                            }
                        }
                    }
                }

                return list;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return null;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="animeID"></param>
        /// <param name="voteValue">Must be 1 or 2 (Anime or Anime Temp(</param>
        /// <param name="voteType"></param>
        public void VoteAnime(int animeID, decimal voteValue, int voteType)
        {
            string msg = $"Voting for anime: {animeID} - Value: {voteValue}";
            logger.Info(msg);

            // lets save to the database and assume it will work
            AniDB_Vote thisVote =
                RepoFactory.AniDB_Vote.GetByEntityAndType(animeID, AniDBVoteType.AnimeTemp) ??
                RepoFactory.AniDB_Vote.GetByEntityAndType(animeID, AniDBVoteType.Anime);

            if (thisVote == null)
            {
                thisVote = new AniDB_Vote
                {
                    EntityID = animeID
                };
            }
            thisVote.VoteType = voteType;

            int iVoteValue = 0;
            if (voteValue > 0)
                iVoteValue = (int) (voteValue * 100);
            else
                iVoteValue = (int) voteValue;

            msg = $"Voting for anime Formatted: {animeID} - Value: {iVoteValue}";
            logger.Info(msg);

            thisVote.VoteValue = iVoteValue;
            RepoFactory.AniDB_Vote.Save(thisVote);

            CommandRequest_VoteAnime cmdVote = new CommandRequest_VoteAnime(animeID, voteType, voteValue);
            cmdVote.Save();
        }

        public void VoteAnimeRevoke(int animeID)
        {
            // lets save to the database and assume it will work

            List<AniDB_Vote> dbVotes = RepoFactory.AniDB_Vote.GetByEntity(animeID);
            AniDB_Vote thisVote = null;
            foreach (AniDB_Vote dbVote in dbVotes)
            {
                // we can only have anime permanent or anime temp but not both
                if (dbVote.VoteType == (int) AniDBVoteType.Anime ||
                    dbVote.VoteType == (int) AniDBVoteType.AnimeTemp)
                {
                    thisVote = dbVote;
                }
            }

            if (thisVote == null) return;

            CommandRequest_VoteAnime cmdVote = new CommandRequest_VoteAnime(animeID, thisVote.VoteType, -1);
            cmdVote.Save();

            RepoFactory.AniDB_Vote.Delete(thisVote.AniDB_VoteID);
        }

        /// <summary>
        /// Set watched status on all normal episodes
        /// </summary>
        /// <param name="animeSeriesID"></param>
        /// <param name="watchedStatus"></param>
        /// <param name="maxEpisodeNumber">Use this to specify a max episode number to apply to</param>
        /// <returns></returns>
        public string SetWatchedStatusOnSeries(int animeSeriesID, bool watchedStatus, int maxEpisodeNumber,
            int episodeType,
            int userID)
        {
            try
            {
                List<SVR_AnimeEpisode> eps = RepoFactory.AnimeEpisode.GetBySeriesID(animeSeriesID);

                SVR_AnimeSeries ser = null;
                foreach (SVR_AnimeEpisode ep in eps)
                {
                    if (ep.EpisodeTypeEnum == (EpisodeType) episodeType &&
                        ep.AniDB_Episode.EpisodeNumber <= maxEpisodeNumber)
                    {
                        // check if this episode is already watched
                        bool currentStatus = false;
                        AnimeEpisode_User epUser = ep.GetUserRecord(userID);
                        if (epUser != null)
                            currentStatus = epUser.WatchedCount > 0 ? true : false;

                        if (currentStatus != watchedStatus)
                        {
                            logger.Info("Updating episode: {0} to {1}", ep.AniDB_Episode.EpisodeNumber, watchedStatus);
                            ep.ToggleWatchedStatus(watchedStatus, true, DateTime.Now, false, userID, false);
                        }
                    }


                    ser = ep.GetAnimeSeries();
                }

                // now update the stats
                if (ser != null)
                {
                    ser.UpdateStats(true, true, true);
                    //StatsCache.Instance.UpdateUsingSeries(ser.AnimeSeriesID);
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public List<CL_AnimeSeries_FileStats> GetSeriesFileStatsByFolderID(int FolderID, int userID, int max)
        {
            try
            {
                int limit = 0;
                Dictionary<int, CL_AnimeSeries_FileStats> list = new Dictionary<int, CL_AnimeSeries_FileStats>();
                foreach (SVR_VideoLocal vi in RepoFactory.VideoLocal.GetByImportFolder(FolderID))
                {
                    foreach (CL_AnimeEpisode_User ae in GetEpisodesForFile(vi.VideoLocalID, userID))
                    {
                        CL_AnimeSeries_User ase = GetSeries(ae.AnimeSeriesID, userID);
                        //check if series is in list if not add it
                        if (list.TryGetValue(ase.AnimeSeriesID, out CL_AnimeSeries_FileStats asfs) == false)
                        {
                            limit++;
                            if (limit >= max)
                            {
                                continue;
                            }
                            asfs = new CL_AnimeSeries_FileStats
                            {
                                AnimeSeriesName = ase.AniDBAnime.AniDBAnime.MainTitle,
                                FileCount = 0,
                                FileSize = 0,
                                Folders = new List<string>(),
                                AnimeSeriesID = ase.AnimeSeriesID
                            };
                            list.Add(ase.AnimeSeriesID, asfs);
                        }

                        asfs.FileCount++;
                        asfs.FileSize += vi.FileSize;

                        //string filePath = Pri.LongPath.Path.GetDirectoryName(vi.FilePath).Replace(importLocation, string.Empty);
                        //filePath = filePath.TrimStart('\\');
                        string filePath = RepoFactory.VideoLocalPlace.GetByVideoLocal(vi.VideoLocalID)[0].FilePath;
                        if (!asfs.Folders.Contains(filePath))
                        {
                            asfs.Folders.Add(filePath);
                        }
                    }
                }

                return list.Values.ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return null;
        }

        public CL_AnimeSeries_User GetSeriesForAnime(int animeID, int userID)
        {
            try
            {
                return RepoFactory.AnimeSeries.GetByAnimeID(animeID)?.GetUserContract(userID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return null;
        }

        public bool GetSeriesExistingForAnime(int animeID)
        {
            try
            {
                SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
                if (series == null)
                    return false;
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return true;
        }

        public List<CL_AnimeGroup_User> GetAllGroups(int userID)
        {
            List<CL_AnimeGroup_User> grps = new List<CL_AnimeGroup_User>();
            try
            {
                return RepoFactory.AnimeGroup.GetAll()
                    .Select(a => a.GetUserContract(userID))
                    .OrderBy(a => a.GroupName)
                    .ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return grps;
        }

        public List<CL_AnimeGroup_User> GetAllGroupsAboveGroupInclusive(int animeGroupID, int userID)
        {
            List<CL_AnimeGroup_User> grps = new List<CL_AnimeGroup_User>();
            try
            {
                int? grpid = animeGroupID;
                while (grpid.HasValue)
                {
                    grpid = null;
                    SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                    if (grp != null)
                    {
                        grps.Add(grp.GetUserContract(userID));
                        grpid = grp.AnimeGroupParentID;
                    }
                }
                return grps;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return grps;
        }

        public List<CL_AnimeGroup_User> GetAllGroupsAboveSeries(int animeSeriesID, int userID)
        {
            List<CL_AnimeGroup_User> grps = new List<CL_AnimeGroup_User>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    SVR_AnimeSeries series = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                    if (series == null)
                        return grps;

                    foreach (SVR_AnimeGroup grp in series.AllGroupsAbove)
                    {
                        grps.Add(grp.GetUserContract(userID));
                    }

                    return grps;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return grps;
        }

        public CL_AnimeGroup_User GetGroup(int animeGroupID, int userID)
        {
            try
            {
                return RepoFactory.AnimeGroup.GetByID(animeGroupID)?.GetUserContract(userID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return null;
        }

        public void RecreateAllGroups(bool resume = false)
        {
            try
            {
                new AnimeGroupCreator().RecreateAllGroups();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public string RenameAllGroups()
        {
            try
            {
                SVR_AnimeGroup.RenameAllGroups();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }

            return string.Empty;
        }

        public string DeleteAnimeGroup(int animeGroupID, bool deleteFiles)
        {
            try
            {
                SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return "Group does not exist";

                int? parentGroupID = grp.AnimeGroupParentID;

                foreach (SVR_AnimeSeries ser in grp.GetAllSeries())
                {
                    DeleteAnimeSeries(ser.AnimeSeriesID, deleteFiles, false);
                }

                // delete all sub groups
                foreach (SVR_AnimeGroup subGroup in grp.GetAllChildGroups())
                {
                    DeleteAnimeGroup(subGroup.AnimeGroupID, deleteFiles);
                }
                List<SVR_GroupFilter> gfs =
                    RepoFactory.GroupFilter.GetWithConditionsTypes(new HashSet<GroupFilterConditionType>()
                    {
                        GroupFilterConditionType.AnimeGroup
                    });
                foreach (SVR_GroupFilter gf in gfs)
                {
                    bool change = false;
                    List<GroupFilterCondition> c =
                        gf.Conditions.Where(a => a.ConditionType == (int) GroupFilterConditionType.AnimeGroup).ToList();
                    foreach (GroupFilterCondition gfc in c)
                    {
                        int.TryParse(gfc.ConditionParameter, out int thisGrpID);
                        if (thisGrpID == animeGroupID)
                        {
                            change = true;
                            gf.Conditions.Remove(gfc);
                        }
                    }
                    if (change)
                    {
                        if (gf.Conditions.Count == 0)
                            RepoFactory.GroupFilter.Delete(gf.GroupFilterID);
                        else
                        {
                            gf.CalculateGroupsAndSeries();
                            RepoFactory.GroupFilter.Save(gf);
                        }
                    }
                }


                RepoFactory.AnimeGroup.Delete(grp.AnimeGroupID);

                // finally update stats

                if (parentGroupID.HasValue)
                {
                    SVR_AnimeGroup grpParent = RepoFactory.AnimeGroup.GetByID(parentGroupID.Value);

                    if (grpParent != null)
                    {
                        grpParent.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
                        //StatsCache.Instance.UpdateUsingGroup(grpParent.TopLevelAnimeGroup.AnimeGroupID);
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public List<CL_AnimeGroup_User> GetAnimeGroupsForFilter(int groupFilterID, int userID,
            bool getSingleSeriesGroups)
        {
            List<CL_AnimeGroup_User> retGroups = new List<CL_AnimeGroup_User>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                    if (user == null) return retGroups;
                    SVR_GroupFilter gf;
                    gf = RepoFactory.GroupFilter.GetByID(groupFilterID);
                    if ((gf != null) && gf.GroupsIds.ContainsKey(userID))
                        retGroups =
                            gf.GroupsIds[userID]
                                .Select(a => RepoFactory.AnimeGroup.GetByID(a))
                                .Where(a => a != null)
                                .Select(a => a.GetUserContract(userID))
                                .ToList();
                    if (getSingleSeriesGroups)
                    {
                        List<CL_AnimeGroup_User> nGroups = new List<CL_AnimeGroup_User>();
                        foreach (CL_AnimeGroup_User cag in retGroups)
                        {
                            CL_AnimeGroup_User ng = cag.DeepCopy();
                            if (cag.Stat_SeriesCount == 1)
                            {
                                if (cag.DefaultAnimeSeriesID.HasValue)
                                    ng.SeriesForNameOverride =
                                        RepoFactory.AnimeSeries.GetByGroupID(ng.AnimeGroupID)
                                            .FirstOrDefault(a => a.AnimeSeriesID == cag.DefaultAnimeSeriesID.Value)
                                            ?
                                            .GetUserContract(userID);
                                if (ng.SeriesForNameOverride == null)
                                    ng.SeriesForNameOverride =
                                        RepoFactory.AnimeSeries.GetByGroupID(ng.AnimeGroupID)
                                            .FirstOrDefault()
                                            ?.GetUserContract(userID);
                            }
                            nGroups.Add(ng);
                        }
                        retGroups = nGroups;
                    }

                    return retGroups;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return retGroups;
        }


        /// <summary>
        /// Can only be used when the group only has one series
        /// </summary>
        /// <param name="animeGroupID"></param>
        /// <param name="allSeries"></param>
        /// <returns></returns>
        public static SVR_AnimeSeries GetSeriesForGroup(int animeGroupID, List<SVR_AnimeSeries> allSeries)
        {
            try
            {
                foreach (SVR_AnimeSeries ser in allSeries)
                {
                    if (ser.AnimeGroupID == animeGroupID) return ser;
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public CL_Response<CL_AnimeGroup_User> SaveGroup(CL_AnimeGroup_Save_Request contract, int userID)
        {
            CL_Response<CL_AnimeGroup_User> contractout = new CL_Response<CL_AnimeGroup_User>
            {
                ErrorMessage = string.Empty,
                Result = null
            };
            try
            {
                SVR_AnimeGroup grp = null;
                if (contract.AnimeGroupID.HasValue && contract.AnimeGroupID != 0)
                {
                    grp = RepoFactory.AnimeGroup.GetByID(contract.AnimeGroupID.Value);
                    if (grp == null)
                    {
                        contractout.ErrorMessage = "Could not find existing group with ID: " +
                                                   contract.AnimeGroupID.Value.ToString();
                        return contractout;
                    }
                }
                else
                {
                    grp = new SVR_AnimeGroup
                    {
                        Description = string.Empty,
                        IsManuallyNamed = 0,
                        DateTimeCreated = DateTime.Now,
                        DateTimeUpdated = DateTime.Now,
                        SortName = string.Empty,
                        MissingEpisodeCount = 0,
                        MissingEpisodeCountGroups = 0,
                        OverrideDescription = 0
                    };
                }

                if (string.IsNullOrEmpty(contract.GroupName))
                {
                    contractout.ErrorMessage = "Must specify a group name";
                    return contractout;
                }

                grp.AnimeGroupParentID = contract.AnimeGroupParentID;
                grp.Description = contract.Description;
                grp.GroupName = contract.GroupName;

                grp.IsManuallyNamed = contract.IsManuallyNamed;
                grp.OverrideDescription = 0;

                if (string.IsNullOrEmpty(contract.SortName))
                    grp.SortName = contract.GroupName;
                else
                    grp.SortName = contract.SortName;

                RepoFactory.AnimeGroup.Save(grp, true, true);

                SVR_AnimeGroup_User userRecord = grp.GetUserRecord(userID);
                if (userRecord == null) userRecord = new SVR_AnimeGroup_User(userID, grp.AnimeGroupID);
                userRecord.IsFave = contract.IsFave;
                RepoFactory.AnimeGroup_User.Save(userRecord);

                contractout.Result = grp.GetUserContract(userID);


                return contractout;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                contractout.ErrorMessage = ex.Message;
                return contractout;
            }
        }

        public CL_Response<CL_AnimeSeries_User> MoveSeries(int animeSeriesID, int newAnimeGroupID, int userID)
        {
            CL_Response<CL_AnimeSeries_User> contractout = new CL_Response<CL_AnimeSeries_User>
            {
                ErrorMessage = string.Empty,
                Result = null
            };
            try
            {
                SVR_AnimeSeries ser = null;

                ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (ser == null)
                {
                    contractout.ErrorMessage = "Could not find existing series with ID: " + animeSeriesID.ToString();
                    return contractout;
                }

                // make sure the group exists
                SVR_AnimeGroup grpTemp = RepoFactory.AnimeGroup.GetByID(newAnimeGroupID);
                if (grpTemp == null)
                {
                    contractout.ErrorMessage = "Could not find existing group with ID: " + newAnimeGroupID.ToString();
                    return contractout;
                }

                int oldGroupID = ser.AnimeGroupID;
                ser.AnimeGroupID = newAnimeGroupID;
                ser.DateTimeUpdated = DateTime.Now;

                //              repSeries.Save(ser,false,false);

                // update stats for new groups
                //ser.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);

                //Update and Save
                ser.UpdateStats(true, true, true);

                // update stats for old groups
                SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(oldGroupID);
                if (grp != null)
                {
                    SVR_AnimeGroup topGroup = grp.TopLevelAnimeGroup;
                    if (grp.GetAllSeries().Count == 0)
                    {
                        RepoFactory.AnimeGroup.Delete(grp.AnimeGroupID);
                    }
                    if (topGroup.AnimeGroupID != grp.AnimeGroupID)
                        topGroup.UpdateStatsFromTopLevel(true, true, true);
                }

                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(ser.AniDB_ID);
                if (anime == null)
                {
                    contractout.ErrorMessage = string.Format("Could not find anime record with ID: {0}", ser.AniDB_ID);
                    return contractout;
                }

                contractout.Result = ser.GetUserContract(userID);

                return contractout;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                contractout.ErrorMessage = ex.Message;
                return contractout;
            }
        }

        public CL_Response<CL_AnimeSeries_User> SaveSeries(CL_AnimeSeries_Save_Request contract, int userID)
        {
            CL_Response<CL_AnimeSeries_User> contractout = new CL_Response<CL_AnimeSeries_User>
            {
                ErrorMessage = string.Empty,
                Result = null
            };
            try
            {
                SVR_AnimeSeries ser = null;

                int? oldGroupID = null;
                if (contract.AnimeSeriesID.HasValue)
                {
                    ser = RepoFactory.AnimeSeries.GetByID(contract.AnimeSeriesID.Value);
                    if (ser == null)
                    {
                        contractout.ErrorMessage = "Could not find existing series with ID: " +
                                                   contract.AnimeSeriesID.Value.ToString();
                        return contractout;
                    }

                    // check if we are moving a series
                    oldGroupID = ser.AnimeGroupID;
                }
                else
                {
                    ser = new SVR_AnimeSeries
                    {
                        DateTimeCreated = DateTime.Now,
                        DefaultAudioLanguage = string.Empty,
                        DefaultSubtitleLanguage = string.Empty,
                        MissingEpisodeCount = 0,
                        MissingEpisodeCountGroups = 0,
                        LatestLocalEpisodeNumber = 0,
                        SeriesNameOverride = string.Empty
                    };
                }


                ser.AnimeGroupID = contract.AnimeGroupID;
                ser.AniDB_ID = contract.AniDB_ID;
                ser.DefaultAudioLanguage = contract.DefaultAudioLanguage;
                ser.DefaultSubtitleLanguage = contract.DefaultSubtitleLanguage;
                ser.DateTimeUpdated = DateTime.Now;
                ser.SeriesNameOverride = contract.SeriesNameOverride;
                ser.DefaultFolder = contract.DefaultFolder;

                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(ser.AniDB_ID);
                if (anime == null)
                {
                    contractout.ErrorMessage = $"Could not find anime record with ID: {ser.AniDB_ID}";
                    return contractout;
                }


                // update stats for groups
                //ser.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true ,true, true);


                //Update and Save
                ser.UpdateStats(true, true, true);

                if (oldGroupID.HasValue)
                {
                    SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(oldGroupID.Value);
                    grp?.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
                }
                contractout.Result = ser.GetUserContract(userID);
                return contractout;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                contractout.ErrorMessage = ex.Message;
                return contractout;
            }
        }

        public CL_Response<CL_AnimeSeries_User> CreateSeriesFromAnime(int animeID, int? animeGroupID, int userID, bool forceOverwrite)
        {
            CL_Response<CL_AnimeSeries_User> response = new CL_Response<CL_AnimeSeries_User>
            {
                Result = null,
                ErrorMessage = string.Empty
            };
            try
            {
                if (animeGroupID.HasValue && animeGroupID.Value > 0)
                {
                    SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID.Value);
                    if (grp == null)
                    {
                        response.ErrorMessage = "Could not find the specified group";
                        return response;
                    }
                }

                // make sure a series doesn't already exists for this anime
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
                if (ser != null && !forceOverwrite)
                {
                    response.ErrorMessage = "A series already exists for this anime";
                    return response;
                }

                // make sure the anime exists first
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);

                AniDB_AnimeUpdate update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(animeID);
                bool animeRecentlyUpdated = false;

                if (update != null)
                {
                    TimeSpan ts = DateTime.Now - update.UpdatedAt;
                    if (ts.TotalHours < 4) animeRecentlyUpdated = true;
                }

                // even if we are missing episode info, don't get data  more than once every 4 hours
                // this is to prevent banning
                if (!animeRecentlyUpdated)
                {
                    logger.Debug("Getting Anime record from AniDB....");
                    anime = ShokoService.AnidbProcessor.GetAnimeInfoHTTP(animeID, true,
                        ServerSettings.AutoGroupSeries || ServerSettings.AniDB_DownloadRelatedAnime);
                }

                if (anime == null)
                {
                    response.ErrorMessage = "Could not get anime information from AniDB";
                    return response;
                }

                logger.Debug("Creating groups, series and episodes....");
                if (ser == null) ser = anime.CreateAnimeSeriesAndGroup(animeGroupID);

                ser.CreateAnimeEpisodes();

                // check if we have any group status data for this associated anime
                // if not we will download it now
                if (RepoFactory.AniDB_GroupStatus.GetByAnimeID(anime.AnimeID).Count == 0)
                {
                    CommandRequest_GetReleaseGroupStatus cmdStatus =
                        new CommandRequest_GetReleaseGroupStatus(anime.AnimeID, false);
                    cmdStatus.Save();
                }

                // update stats
                RepoFactory.AnimeSeries.Save(ser, false, false);

                foreach (SVR_AnimeGroup grp in ser.AllGroupsAbove)
                {
                    RepoFactory.AnimeGroup.Save(grp, true, false);
                }

                response.Result = ser.GetUserContract(userID);
                return response;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                response.ErrorMessage = ex.Message;
            }

            return response;
        }

        public string UpdateAnimeData(int animeID)
        {
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    ShokoService.AnidbProcessor.GetAnimeInfoHTTP(session, animeID, true, false);

                    // also find any files for this anime which don't have proper media info data
                    // we can usually tell this if the Resolution == '0x0'
                    foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID))
                    {
                        AniDB_File aniFile = vid.GetAniDBFile();
                        if (aniFile == null) continue;

                        if (aniFile.File_VideoResolution.Equals("0x0", StringComparison.InvariantCultureIgnoreCase))
                        {
                            CommandRequest_GetFile cmd = new CommandRequest_GetFile(vid.VideoLocalID, true);
                            cmd.Save(session);
                        }
                    }

                    // update group status information
                    CommandRequest_GetReleaseGroupStatus cmdStatus = new CommandRequest_GetReleaseGroupStatus(animeID,
                        true);
                    cmdStatus.Save(session);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return string.Empty;
        }

        public void UpdateAnimeDisableExternalLinksFlag(int animeID, int flags)
        {
            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return;

                anime.DisableExternalLinksFlag = flags;
                RepoFactory.AniDB_Anime.Save(anime);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public void SetDefaultSeriesForGroup(int animeGroupID, int animeSeriesID)
        {
            try
            {
                SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return;

                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (ser == null) return;

                grp.DefaultAnimeSeriesID = animeSeriesID;
                RepoFactory.AnimeGroup.Save(grp, false, false);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public void RemoveDefaultSeriesForGroup(int animeGroupID)
        {
            try
            {
                SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return;

                grp.DefaultAnimeSeriesID = null;
                RepoFactory.AnimeGroup.Save(grp, false, false);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public CL_AnimeGroup_User GetTopLevelGroupForSeries(int animeSeriesID, int userID)
        {
            try
            {
                return RepoFactory.AnimeSeries.GetByID(animeSeriesID)?.TopLevelAnimeGroup?.GetUserContract(userID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return null;
        }

        public void IgnoreAnime(int animeID, int ignoreType, int userID)
        {
            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return;

                SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                if (user == null) return;

                IgnoreAnime ignore = RepoFactory.IgnoreAnime.GetByAnimeUserType(animeID, userID, ignoreType);
                if (ignore != null) return; // record already exists

                ignore = new IgnoreAnime
                {
                    AnimeID = animeID,
                    IgnoreType = ignoreType,
                    JMMUserID = userID
                };
                RepoFactory.IgnoreAnime.Save(ignore);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public List<CL_AniDB_Anime_Similar> GetSimilarAnimeLinks(int animeID, int userID)
        {
            List<CL_AniDB_Anime_Similar> links = new List<CL_AniDB_Anime_Similar>();
            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return links;

                SVR_JMMUser juser = RepoFactory.JMMUser.GetByID(userID);
                if (juser == null) return links;


                foreach (AniDB_Anime_Similar link in anime.GetSimilarAnime())
                {
                    SVR_AniDB_Anime animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(link.SimilarAnimeID);
                    if (animeLink != null)
                    {
                        if (!juser.AllowedAnime(animeLink)) continue;
                    }

                    // check if this anime has a series
                    SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(link.SimilarAnimeID);

                    links.Add(link.ToClient(animeLink, ser, userID));
                }

                return links;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return links;
            }
        }

        public List<CL_AniDB_Anime_Relation> GetRelatedAnimeLinks(int animeID, int userID)
        {
            List<CL_AniDB_Anime_Relation> links = new List<CL_AniDB_Anime_Relation>();
            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return links;

                SVR_JMMUser juser = RepoFactory.JMMUser.GetByID(userID);
                if (juser == null) return links;


                foreach (AniDB_Anime_Relation link in anime.GetRelatedAnime())
                {
                    SVR_AniDB_Anime animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(link.RelatedAnimeID);
                    if (animeLink != null)
                    {
                        if (!juser.AllowedAnime(animeLink)) continue;
                    }

                    // check if this anime has a series
                    SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(link.RelatedAnimeID);

                    links.Add(link.ToClient(animeLink, ser, userID));
                }

                return links;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return links;
            }
        }

        /// <summary>
        /// Delete a series, and everything underneath it (episodes, files)
        /// </summary>
        /// <param name="animeSeriesID"></param>
        /// <param name="deleteFiles">also delete the physical files</param>
        /// <returns></returns>
        public string DeleteAnimeSeries(int animeSeriesID, bool deleteFiles, bool deleteParentGroup)
        {
            try
            {
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(animeSeriesID);
                if (ser == null) return "Series does not exist";

                int animeGroupID = ser.AnimeGroupID;

                foreach (SVR_AnimeEpisode ep in ser.GetAnimeEpisodes())
                {
                    foreach (SVR_VideoLocal vid in ep.GetVideoLocals())
                    {
                        foreach (SVR_VideoLocal_Place place in vid.Places)
                        {
                            if (deleteFiles)
                            {
                                (bool success, string result) = place.RemoveAndDeleteFile();
                                if (!success) return result;
                            }
                            else
                            {
                                place.RemoveRecord();
                            }
                        }
                    }
                    RepoFactory.AnimeEpisode.Delete(ep.AnimeEpisodeID);
                }
                RepoFactory.AnimeSeries.Delete(ser.AnimeSeriesID);

                // finally update stats
                SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp != null)
                {
                    if (grp.GetAllSeries().Count == 0)
                    {
                        DeleteAnimeGroup(grp.AnimeGroupID, false);
                    }
                    else
                    {
                        grp.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, true);
                        //StatsCache.Instance.UpdateUsingGroup(grp.TopLevelAnimeGroup.AnimeGroupID);
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public CL_AniDB_Anime GetAnime(int animeID)
        {
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(session.Wrap(), animeID);
                    return anime?.Contract.AniDBAnime;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return null;
        }

        public List<CL_AniDB_Anime> GetAllAnime()
        {
            try
            {
                return RepoFactory.AniDB_Anime.GetAll().Select(a => a.Contract.AniDBAnime).ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return new List<CL_AniDB_Anime>();
        }

        public List<CL_AnimeRating> GetAnimeRatings(int collectionState, int watchedState, int ratingVotedState,
            int userID)
        {
            List<CL_AnimeRating> contracts = new List<CL_AnimeRating>();

            try
            {
                IReadOnlyList<SVR_AnimeSeries> series = RepoFactory.AnimeSeries.GetAll();
                Dictionary<int, SVR_AnimeSeries> dictSeries = new Dictionary<int, SVR_AnimeSeries>();
                foreach (SVR_AnimeSeries ser in series)
                    dictSeries[ser.AniDB_ID] = ser;

                RatingCollectionState _collectionState = (RatingCollectionState) collectionState;
                RatingWatchedState _watchedState = (RatingWatchedState) watchedState;
                RatingVotedState _ratingVotedState = (RatingVotedState) ratingVotedState;

                IReadOnlyList<SVR_AniDB_Anime> animes = RepoFactory.AniDB_Anime.GetAll();

                // user votes
                IReadOnlyList<AniDB_Vote> allVotes = RepoFactory.AniDB_Vote.GetAll();

                SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                if (user == null) return contracts;

                int i = 0;


                foreach (SVR_AniDB_Anime anime in animes)
                {
                    i++;

                    // evaluate collection states
                    if (_collectionState == RatingCollectionState.AllEpisodesInMyCollection)
                    {
                        if (!anime.GetFinishedAiring()) continue;
                        if (!dictSeries.ContainsKey(anime.AnimeID)) continue;
                        if (dictSeries[anime.AnimeID].MissingEpisodeCount > 0) continue;
                    }

                    if (_collectionState == RatingCollectionState.InMyCollection)
                        if (!dictSeries.ContainsKey(anime.AnimeID)) continue;

                    if (_collectionState == RatingCollectionState.NotInMyCollection)
                        if (dictSeries.ContainsKey(anime.AnimeID)) continue;

                    if (!user.AllowedAnime(anime)) continue;

                    // evaluate watched states
                    if (_watchedState == RatingWatchedState.AllEpisodesWatched)
                    {
                        if (!dictSeries.ContainsKey(anime.AnimeID)) continue;
                        AnimeSeries_User userRec = dictSeries[anime.AnimeID].GetUserRecord(userID);
                        if (userRec == null) continue;
                        if (userRec.UnwatchedEpisodeCount > 0) continue;
                    }

                    if (_watchedState == RatingWatchedState.NotWatched)
                    {
                        if (dictSeries.ContainsKey(anime.AnimeID))
                        {
                            AnimeSeries_User userRec = dictSeries[anime.AnimeID].GetUserRecord(userID);
                            if (userRec != null)
                            {
                                if (userRec.UnwatchedEpisodeCount == 0) continue;
                            }
                        }
                    }

                    // evaluate voted states
                    if (_ratingVotedState == RatingVotedState.Voted)
                    {
                        bool voted = false;
                        foreach (AniDB_Vote vote in allVotes)
                        {
                            if (vote.EntityID == anime.AnimeID &&
                                (vote.VoteType == (int) AniDBVoteType.Anime ||
                                 vote.VoteType == (int) AniDBVoteType.AnimeTemp))
                            {
                                voted = true;
                                break;
                            }
                        }

                        if (!voted) continue;
                    }

                    if (_ratingVotedState == RatingVotedState.NotVoted)
                    {
                        bool voted = false;
                        foreach (AniDB_Vote vote in allVotes)
                        {
                            if (vote.EntityID == anime.AnimeID &&
                                (vote.VoteType == (int) AniDBVoteType.Anime ||
                                 vote.VoteType == (int) AniDBVoteType.AnimeTemp))
                            {
                                voted = true;
                                break;
                            }
                        }

                        if (voted) continue;
                    }

                    CL_AnimeRating contract = new CL_AnimeRating
                    {
                        AnimeID = anime.AnimeID,
                        AnimeDetailed = anime.Contract
                    };
                    if (dictSeries.ContainsKey(anime.AnimeID))
                    {
                        contract.AnimeSeries = dictSeries[anime.AnimeID].GetUserContract(userID);
                    }

                    contracts.Add(contract);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return contracts;
        }

        public List<CL_AniDB_AnimeDetailed> GetAllAnimeDetailed()
        {
            try
            {
                return RepoFactory.AniDB_Anime.GetAll().Select(a => a.Contract).ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return new List<CL_AniDB_AnimeDetailed>();
        }

        public List<CL_AnimeSeries_User> GetAllSeries(int userID)
        {
            try
            {
                return RepoFactory.AnimeSeries.GetAll().Select(a => a.GetUserContract(userID)).ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return new List<CL_AnimeSeries_User>();
        }

        public CL_AniDB_AnimeDetailed GetAnimeDetailed(int animeID)
        {
            try
            {
                return RepoFactory.AniDB_Anime.GetByAnimeID(animeID)?.Contract;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public List<CL_AnimeGroup_User> GetSubGroupsForGroup(int animeGroupID, int userID)
        {
            List<CL_AnimeGroup_User> retGroups = new List<CL_AnimeGroup_User>();
            try
            {
                SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return retGroups;
                foreach (SVR_AnimeGroup grpChild in grp.GetChildGroups())
                {
                    CL_AnimeGroup_User ugrp = grpChild.GetUserContract(userID);
                    if (ugrp != null)
                        retGroups.Add(ugrp);
                }

                return retGroups;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return retGroups;
        }

        public List<CL_AnimeSeries_User> GetSeriesForGroup(int animeGroupID, int userID)
        {
            List<CL_AnimeSeries_User> series = new List<CL_AnimeSeries_User>();
            try
            {
                SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return series;

                foreach (SVR_AnimeSeries ser in grp.GetSeries())
                {
                    CL_AnimeSeries_User s = ser.GetUserContract(userID);
                    if (s != null)
                        series.Add(s);
                }

                return series;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return series;
            }
        }

        public List<CL_AnimeSeries_User> GetSeriesForGroupRecursive(int animeGroupID, int userID)
        {
            List<CL_AnimeSeries_User> series = new List<CL_AnimeSeries_User>();
            try
            {
                SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
                if (grp == null) return series;

                foreach (SVR_AnimeSeries ser in grp.GetAllSeries())
                {
                    CL_AnimeSeries_User s = ser.GetUserContract(userID);
                    if (s != null)
                        series.Add(s);
                }

                return series;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return series;
            }
        }

        #endregion

        #region Group Filters

        public CL_Response<CL_GroupFilter> SaveGroupFilter(CL_GroupFilter contract)
        {
            CL_Response<CL_GroupFilter> response = new CL_Response<CL_GroupFilter>
            {
                ErrorMessage = string.Empty,
                Result = null
            };


            // Process the group
            SVR_GroupFilter gf;
            if (contract.GroupFilterID != 0)
            {
                gf = RepoFactory.GroupFilter.GetByID(contract.GroupFilterID);
                if (gf == null)
                {
                    response.ErrorMessage = "Could not find existing Group Filter with ID: " +
                                            contract.GroupFilterID.ToString();
                    return response;
                }
            }

            gf = SVR_GroupFilter.FromClient(contract);

            gf.CalculateGroupsAndSeries();
            RepoFactory.GroupFilter.Save(gf);
            response.Result = gf.ToClient();
            return response;
        }

        public string DeleteGroupFilter(int groupFilterID)
        {
            try
            {
                SVR_GroupFilter gf = RepoFactory.GroupFilter.GetByID(groupFilterID);
                if (gf == null)
                    return "Group Filter not found";

                RepoFactory.GroupFilter.Delete(groupFilterID);

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public CL_GroupFilterExtended GetGroupFilterExtended(int groupFilterID, int userID)
        {
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    SVR_GroupFilter gf = RepoFactory.GroupFilter.GetByID(groupFilterID);
                    if (gf == null) return null;

                    SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                    if (user == null) return null;

                    CL_GroupFilterExtended contract = gf.ToClientExtended(user);

                    return contract;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return null;
        }

        public List<CL_GroupFilterExtended> GetAllGroupFiltersExtended(int userID)
        {
            List<CL_GroupFilterExtended> gfs = new List<CL_GroupFilterExtended>();
            try
            {
                SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                if (user == null) return gfs;
                IReadOnlyList<SVR_GroupFilter> allGfs = RepoFactory.GroupFilter.GetAll();
                foreach (SVR_GroupFilter gf in allGfs)
                {
                    CL_GroupFilter gfContract = gf.ToClient();
                    CL_GroupFilterExtended gfeContract = new CL_GroupFilterExtended
                    {
                        GroupFilter = gfContract,
                        GroupCount = 0,
                        SeriesCount = 0
                    };
                    if (gf.GroupsIds.ContainsKey(user.JMMUserID))
                        gfeContract.GroupCount = gf.GroupsIds.Count;
                    gfs.Add(gfeContract);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return gfs;
        }

        public List<CL_GroupFilterExtended> GetGroupFiltersExtended(int userID, int gfparentid = 0)
        {
            List<CL_GroupFilterExtended> gfs = new List<CL_GroupFilterExtended>();
            try
            {
                SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                if (user == null) return gfs;
                List<SVR_GroupFilter> allGfs = gfparentid == 0
                    ? RepoFactory.GroupFilter.GetTopLevel()
                    : RepoFactory.GroupFilter.GetByParentID(gfparentid);
                foreach (SVR_GroupFilter gf in allGfs)
                {
                    CL_GroupFilter gfContract = gf.ToClient();
                    CL_GroupFilterExtended gfeContract = new CL_GroupFilterExtended
                    {
                        GroupFilter = gfContract,
                        GroupCount = 0,
                        SeriesCount = 0
                    };
                    if (gf.GroupsIds.ContainsKey(user.JMMUserID))
                        gfeContract.GroupCount = gf.GroupsIds.Count;
                    gfs.Add(gfeContract);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return gfs;
        }

        public List<CL_GroupFilter> GetAllGroupFilters()
        {
            List<CL_GroupFilter> gfs = new List<CL_GroupFilter>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    DateTime start = DateTime.Now;

                    IReadOnlyList<SVR_GroupFilter> allGfs = RepoFactory.GroupFilter.GetAll();
                    TimeSpan ts = DateTime.Now - start;
                    logger.Info("GetAllGroupFilters (Database) in {0} ms", ts.TotalMilliseconds);

                    start = DateTime.Now;
                    foreach (SVR_GroupFilter gf in allGfs)
                    {
                        gfs.Add(gf.ToClient());
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return gfs;
        }

        public List<CL_GroupFilter> GetGroupFilters(int gfparentid = 0)
        {
            List<CL_GroupFilter> gfs = new List<CL_GroupFilter>();
            try
            {
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    DateTime start = DateTime.Now;

                    List<SVR_GroupFilter> allGfs = gfparentid == 0
                        ? RepoFactory.GroupFilter.GetTopLevel()
                        : RepoFactory.GroupFilter.GetByParentID(gfparentid);
                    TimeSpan ts = DateTime.Now - start;
                    logger.Info("GetAllGroupFilters (Database) in {0} ms", ts.TotalMilliseconds);

                    start = DateTime.Now;
                    foreach (SVR_GroupFilter gf in allGfs)
                    {
                        gfs.Add(gf.ToClient());
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return gfs;
        }

        public CL_GroupFilter GetGroupFilter(int gf)
        {
            try
            {
                return RepoFactory.GroupFilter.GetByID(gf)?.ToClient();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return null;
        }

        public CL_GroupFilter EvaluateGroupFilter(CL_GroupFilter contract)
        {
            try
            {
                return SVR_GroupFilter.EvaluateContract(contract);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new CL_GroupFilter();
            }
        }

        #endregion

        #region Playlists

        public List<Playlist> GetAllPlaylists()
        {
            try
            {
                return RepoFactory.Playlist.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return new List<Playlist>();
        }

        public CL_Response<Playlist> SavePlaylist(Playlist contract)
        {
            CL_Response<Playlist> contractRet = new CL_Response<Playlist>
            {
                ErrorMessage = string.Empty
            };
            try
            {
                // Process the playlist
                Playlist pl = null;
                if (contract.PlaylistID != 0)
                {
                    pl = RepoFactory.Playlist.GetByID(contract.PlaylistID);
                    if (pl == null)
                    {
                        contractRet.ErrorMessage = "Could not find existing Playlist with ID: " +
                                                   contract.PlaylistID.ToString();
                        return contractRet;
                    }
                }
                else
                    pl = new Playlist();

                if (string.IsNullOrEmpty(contract.PlaylistName))
                {
                    contractRet.ErrorMessage = "Playlist must have a name";
                    return contractRet;
                }

                pl.DefaultPlayOrder = contract.DefaultPlayOrder;
                pl.PlaylistItems = contract.PlaylistItems;
                pl.PlaylistName = contract.PlaylistName;
                pl.PlayUnwatched = contract.PlayUnwatched;
                pl.PlayWatched = contract.PlayWatched;

                RepoFactory.Playlist.Save(pl);

                contractRet.Result = pl;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                contractRet.ErrorMessage = ex.Message;
                return contractRet;
            }

            return contractRet;
        }

        public string DeletePlaylist(int playlistID)
        {
            try
            {
                Playlist pl = RepoFactory.Playlist.GetByID(playlistID);
                if (pl == null)
                    return "Playlist not found";

                RepoFactory.Playlist.Delete(playlistID);

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public Playlist GetPlaylist(int playlistID)
        {
            try
            {
                return RepoFactory.Playlist.GetByID(playlistID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        #endregion

        #region Custom Tags

        public List<CustomTag> GetAllCustomTags()
        {
            try
            {
                return RepoFactory.CustomTag.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        public CL_Response<CrossRef_CustomTag> SaveCustomTagCrossRef(CrossRef_CustomTag contract)
        {
            CL_Response<CrossRef_CustomTag> contractRet = new CL_Response<CrossRef_CustomTag>
            {
                ErrorMessage = string.Empty
            };
            try
            {
                // this is an update
                CrossRef_CustomTag xref = null;
                if (contract.CrossRef_CustomTagID != 0)
                {
                    contractRet.ErrorMessage = "Updates are not allowed";
                    return contractRet;
                }
                else
                    xref = new CrossRef_CustomTag();

                //TODO: Custom Tags - check if the CustomTagID is valid
                //TODO: Custom Tags - check if the CrossRefID is valid

                xref.CrossRefID = contract.CrossRefID;
                xref.CrossRefType = contract.CrossRefType;
                xref.CustomTagID = contract.CustomTagID;

                RepoFactory.CrossRef_CustomTag.Save(xref);

                contractRet.Result = xref;
                SVR_AniDB_Anime.UpdateStatsByAnimeID(contract.CrossRefID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                contractRet.ErrorMessage = ex.Message;
                return contractRet;
            }

            return contractRet;
        }

        public string DeleteCustomTagCrossRefByID(int xrefID)
        {
            try
            {
                CrossRef_CustomTag pl = RepoFactory.CrossRef_CustomTag.GetByID(xrefID);
                if (pl == null)
                    return "Custom Tag not found";

                RepoFactory.CrossRef_CustomTag.Delete(xrefID);

                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public string DeleteCustomTagCrossRef(int customTagID, int crossRefType, int crossRefID)
        {
            try
            {
                List<CrossRef_CustomTag> xrefs =
                    RepoFactory.CrossRef_CustomTag.GetByUniqueID(customTagID, crossRefType, crossRefID);

                if (xrefs == null || xrefs.Count == 0)
                    return "Custom Tag not found";

                RepoFactory.CrossRef_CustomTag.Delete(xrefs[0].CrossRef_CustomTagID);
                SVR_AniDB_Anime.UpdateStatsByAnimeID(crossRefID);
                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public CL_Response<CustomTag> SaveCustomTag(CustomTag contract)
        {
            CL_Response<CustomTag> contractRet = new CL_Response<CustomTag>
            {
                ErrorMessage = string.Empty
            };
            try
            {
                // this is an update
                CustomTag ctag = null;
                if (contract.CustomTagID != 0)
                {
                    ctag = RepoFactory.CustomTag.GetByID(contract.CustomTagID);
                    if (ctag == null)
                    {
                        contractRet.ErrorMessage = "Could not find existing custom tag with ID: " +
                                                   contract.CustomTagID.ToString();
                        return contractRet;
                    }
                }
                else
                    ctag = new CustomTag();

                if (string.IsNullOrEmpty(contract.TagName))
                {
                    contractRet.ErrorMessage = "Custom Tag must have a name";
                    return contractRet;
                }

                ctag.TagName = contract.TagName;
                ctag.TagDescription = contract.TagDescription;

                RepoFactory.CustomTag.Save(ctag);

                contractRet.Result = ctag;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                contractRet.ErrorMessage = ex.Message;
                return contractRet;
            }

            return contractRet;
        }

        public string DeleteCustomTag(int customTagID)
        {
            try
            {
                CustomTag pl = RepoFactory.CustomTag.GetByID(customTagID);
                if (pl == null)
                    return "Custom Tag not found";

                // first get a list of all the anime that referenced this tag
                List<CrossRef_CustomTag> xrefs = RepoFactory.CrossRef_CustomTag.GetByCustomTagID(customTagID);

                RepoFactory.CustomTag.Delete(customTagID);

                // update cached data for any anime that were affected
                foreach (CrossRef_CustomTag xref in xrefs)
                {
                    SVR_AniDB_Anime.UpdateStatsByAnimeID(xref.CrossRefID);
                }


                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public CustomTag GetCustomTag(int customTagID)
        {
            try
            {
                return RepoFactory.CustomTag.GetByID(customTagID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return null;
            }
        }

        #endregion

        #region Users

        public List<JMMUser> GetAllUsers()
        {
            try
            {
                return RepoFactory.JMMUser.GetAll().Cast<JMMUser>().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<JMMUser>();
            }
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

        public string ChangePassword(int userID, string newPassword)
        {
            return ChangePassword(userID, newPassword, true);
        }

        public string ChangePassword(int userID, string newPassword, bool revokeapikey)
        {
            try
            {
                SVR_JMMUser jmmUser = RepoFactory.JMMUser.GetByID(userID);
                if (jmmUser == null) return "User not found";

                jmmUser.Password = Digest.Hash(newPassword);
                RepoFactory.JMMUser.Save(jmmUser, false);
                if (revokeapikey)
                {
                    RepoFactory.AuthTokens.DeleteAllWithUserID(jmmUser.JMMUserID);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }

            return string.Empty;
        }

        public string SaveUser(JMMUser user)
        {
            try
            {
                bool existingUser = false;
                bool updateStats = false;
                bool updateGf = false;
                SVR_JMMUser jmmUser = null;
                if (user.JMMUserID != 0)
                {
                    jmmUser = RepoFactory.JMMUser.GetByID(user.JMMUserID);
                    if (jmmUser == null) return "User not found";
                    existingUser = true;
                }
                else
                {
                    jmmUser = new SVR_JMMUser();
                    updateStats = true;
                    updateGf = true;
                }

                if (existingUser && jmmUser.IsAniDBUser != user.IsAniDBUser)
                    updateStats = true;

                string hcat = string.Join(",", user.HideCategories);
                if (jmmUser.HideCategories != hcat)
                    updateGf = true;
                jmmUser.HideCategories = hcat;
                jmmUser.IsAniDBUser = user.IsAniDBUser;
                jmmUser.IsTraktUser = user.IsTraktUser;
                jmmUser.IsAdmin = user.IsAdmin;
                jmmUser.Username = user.Username;
                jmmUser.CanEditServerSettings = user.CanEditServerSettings;
                jmmUser.PlexUsers = string.Join(",", user.PlexUsers);
                jmmUser.PlexToken = user.PlexToken;
                if (string.IsNullOrEmpty(user.Password))
                {
                    jmmUser.Password = string.Empty;
                }
                else
                {
                    // Additional check for hashed password, if not hashed we hash it
                    if (user.Password.Length < 64)
                        jmmUser.Password = Digest.Hash(user.Password);
                    else
                        jmmUser.Password = user.Password;
                }

                // make sure that at least one user is an admin
                if (jmmUser.IsAdmin == 0)
                {
                    bool adminExists = false;
                    IReadOnlyList<SVR_JMMUser> users = RepoFactory.JMMUser.GetAll();
                    foreach (SVR_JMMUser userOld in users)
                    {
                        if (userOld.IsAdmin == 1)
                        {
                            if (existingUser)
                            {
                                if (userOld.JMMUserID != jmmUser.JMMUserID) adminExists = true;
                            }
                            else
                            {
                                //one admin account is needed
                                adminExists = true;
                                break;
                            }
                        }
                    }

                    if (!adminExists) return "At least one user must be an administrator";
                }

                RepoFactory.JMMUser.Save(jmmUser, updateGf);

                // update stats
                if (updateStats)
                {
                    foreach (SVR_AnimeSeries ser in RepoFactory.AnimeSeries.GetAll())
                        ser.QueueUpdateStats();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }

            return string.Empty;
        }

        public string DeleteUser(int userID)
        {
            try
            {
                SVR_JMMUser jmmUser = RepoFactory.JMMUser.GetByID(userID);
                if (jmmUser == null) return "User not found";

                // make sure that at least one user is an admin
                if (jmmUser.IsAdmin == 1)
                {
                    bool adminExists = false;
                    IReadOnlyList<SVR_JMMUser> users = RepoFactory.JMMUser.GetAll();
                    foreach (SVR_JMMUser userOld in users)
                    {
                        if (userOld.IsAdmin == 1)
                        {
                            if (userOld.JMMUserID != jmmUser.JMMUserID) adminExists = true;
                        }
                    }

                    if (!adminExists) return "At least one user must be an administrator";
                }

                RepoFactory.JMMUser.Delete(userID);

                // delete all user records
                RepoFactory.AnimeSeries_User.Delete(RepoFactory.AnimeSeries_User.GetByUserID(userID));
                RepoFactory.AnimeGroup_User.Delete(RepoFactory.AnimeGroup_User.GetByUserID(userID));
                RepoFactory.AnimeEpisode_User.Delete(RepoFactory.AnimeEpisode_User.GetByUserID(userID));
                RepoFactory.VideoLocalUser.Delete(RepoFactory.VideoLocalUser.GetByUserID(userID));
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }

            return string.Empty;
        }

        #endregion

        #region Import Folders
        public List<ImportFolder> GetImportFolders()
        {
            try
            {
                return RepoFactory.ImportFolder.GetAll().Cast<ImportFolder>().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return new List<ImportFolder>();
        }

        public CL_Response<ImportFolder> SaveImportFolder(ImportFolder contract)
        {
            CL_Response<ImportFolder> response = new CL_Response<ImportFolder>
            {
                ErrorMessage = string.Empty,
                Result = null
            };
            try
            {
                SVR_ImportFolder ns = null;
                if (contract.ImportFolderID != 0)
                {
                    // update
                    ns = RepoFactory.ImportFolder.GetByID(contract.ImportFolderID);
                    if (ns == null)
                    {
                        response.ErrorMessage = "Could not find Import Folder ID: " +
                                                contract.ImportFolderID.ToString();
                        return response;
                    }
                }
                else
                {
                    // create
                    ns = new SVR_ImportFolder();
                }

                if (string.IsNullOrEmpty(contract.ImportFolderName))
                {
                    response.ErrorMessage = "Must specify an Import Folder name";
                    return response;
                }

                if (string.IsNullOrEmpty(contract.ImportFolderLocation))
                {
                    response.ErrorMessage = "Must specify an Import Folder location";
                    return response;
                }

                if (contract.CloudID == null && !Directory.Exists(contract.ImportFolderLocation))
                {
                    response.ErrorMessage = "Cannot find Import Folder location";
                    return response;
                }

                if (contract.ImportFolderID == 0)
                {
                    SVR_ImportFolder nsTemp =
                        RepoFactory.ImportFolder.GetByImportLocation(contract.ImportFolderLocation);
                    if (nsTemp != null)
                    {
                        response.ErrorMessage = "An entry already exists for the specified Import Folder location";
                        return response;
                    }
                }

                if (contract.IsDropDestination == 1 && contract.IsDropSource == 1)
                {
                    response.ErrorMessage = "A folder cannot be a drop source and a drop destination at the same time";
                    return response;
                }

                // check to make sure we don't have multiple drop folders
                IReadOnlyList<SVR_ImportFolder> allFolders = RepoFactory.ImportFolder.GetAll();

                if (contract.IsDropDestination == 1)
                {
                    foreach (SVR_ImportFolder imf in allFolders)
                    {
                        if (contract.CloudID == imf.CloudID && imf.IsDropDestination == 1 &&
                            (contract.ImportFolderID == 0 || contract.ImportFolderID != imf.ImportFolderID))
                        {
                            imf.IsDropDestination = 0;
                            RepoFactory.ImportFolder.Save(imf);
                        }
                        else if (imf.CloudID != contract.CloudID)
                        {
                            if (contract.IsDropSource == 1 && (imf.FolderIsDropDestination || imf.FolderIsDropSource))
                            {
                                response.ErrorMessage = "A drop folders cannot have different file systems";
                                return response;
                            }
                            if (contract.IsDropDestination == 1 && (imf.FolderIsDropDestination || imf.FolderIsDropSource))
                            {
                                response.ErrorMessage = "A drop folders cannot have different file systems";
                                return response;
                            }
                        }
                    }
                }

                ns.ImportFolderName = contract.ImportFolderName;
                ns.ImportFolderLocation = contract.ImportFolderLocation;
                ns.IsDropDestination = contract.IsDropDestination;
                ns.IsDropSource = contract.IsDropSource;
                ns.IsWatched = contract.IsWatched;
                ns.ImportFolderType = contract.ImportFolderType;
                ns.CloudID = contract.CloudID.HasValue && contract.CloudID == 0 ? null : contract.CloudID;

                RepoFactory.ImportFolder.Save(ns);

                response.Result = ns;
                Utils.MainThreadDispatch(() =>
                {
                    ServerInfo.Instance.RefreshImportFolders();
                });
                ShokoServer.StopWatchingFiles();
                ShokoServer.StartWatchingFiles();

                return response;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                response.ErrorMessage = ex.Message;
                return response;
            }
        }

        public string DeleteImportFolder(int importFolderID)
        {
            ShokoServer.DeleteImportFolder(importFolderID);
            return string.Empty;
        }
        #endregion
    }
}