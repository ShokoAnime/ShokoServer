using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AniDBAPI;
using JMMContracts;
using JMMServer.Commands;
using JMMServer.Repositories;
using NHibernate;
using NLog;

namespace JMMServer.Entities
{
    public class AnimeSeries
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public string Year
        {
            get { return GetAnime().Year; }
        }

        public string GenresRaw
        {
            get
            {
                if (GetAnime() == null)
                    return "";
                return GetAnime().TagsString;
            }
        }

        public CrossRef_AniDB_Other CrossRefMovieDB
        {
            get
            {
                var repCrossRef = new CrossRef_AniDB_OtherRepository();
                return repCrossRef.GetByAnimeIDAndType(AniDB_ID, CrossRefType.MovieDB);
            }
        }

        public List<CrossRef_AniDB_MAL> CrossRefMAL
        {
            get
            {
                var repCrossRef = new CrossRef_AniDB_MALRepository();
                return repCrossRef.GetByAnimeID(AniDB_ID);
            }
        }

        public DateTime? AirDate
        {
            get
            {
                if (GetAnime() != null)
                    return GetAnime().AirDate;
                return DateTime.Now;
            }
        }

        public DateTime? EndDate
        {
            get
            {
                if (GetAnime() != null)
                    return GetAnime().EndDate;
                return null;
            }
        }

        /// <summary>
        ///     Gets the direct parent AnimeGroup this series belongs to
        /// </summary>
        public AnimeGroup AnimeGroup
        {
            get
            {
                var repGroups = new AnimeGroupRepository();
                return repGroups.GetByID(AnimeGroupID);
            }
        }

        /// <summary>
        ///     Gets the very top level AnimeGroup which this series belongs to
        /// </summary>
        public AnimeGroup TopLevelAnimeGroup
        {
            get
            {
                var repGroups = new AnimeGroupRepository();
                var parentGroup = repGroups.GetByID(AnimeGroupID);

                while (parentGroup.AnimeGroupParentID.HasValue)
                {
                    parentGroup = repGroups.GetByID(parentGroup.AnimeGroupParentID.Value);
                }
                return parentGroup;
            }
        }

        public List<AnimeGroup> AllGroupsAbove
        {
            get
            {
                var grps = new List<AnimeGroup>();
                try
                {
                    var repGroups = new AnimeGroupRepository();
                    var repSeries = new AnimeSeriesRepository();

                    int? groupID = AnimeGroupID;
                    while (groupID.HasValue)
                    {
                        var grp = repGroups.GetByID(groupID.Value);
                        if (grp != null)
                        {
                            grps.Add(grp);
                            groupID = grp.AnimeGroupParentID;
                        }
                        else
                        {
                            groupID = null;
                        }
                    }

                    return grps;
                }
                catch (Exception ex)
                {
                    logger.ErrorException(ex.ToString(), ex);
                }
                return grps;
            }
        }

        public string GetSeriesName()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetSeriesName(session);
            }
        }

        public string GetSeriesName(ISession session)
        {
            var seriesName = "";
            if (!string.IsNullOrEmpty(SeriesNameOverride))
                seriesName = SeriesNameOverride;
            else
            {
                if (ServerSettings.SeriesNameSource == DataSourceType.AniDB)
                    seriesName = GetAnime(session).GetFormattedTitle();
                else
                {
                    var tvdbs = GetTvDBSeries(session);

                    if (tvdbs != null && tvdbs.Count > 0 && !string.IsNullOrEmpty(tvdbs[0].SeriesName) &&
                        !tvdbs[0].SeriesName.ToUpper().Contains("**DUPLICATE"))
                        seriesName = tvdbs[0].SeriesName;
                    else
                        seriesName = GetAnime(session).GetFormattedTitle(session);
                }
            }

            return seriesName;
        }


        public List<AnimeEpisode> GetAnimeEpisodes()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetAnimeEpisodes(session);
            }
        }

        public List<AnimeEpisode> GetAnimeEpisodes(ISession session)
        {
            var repEpisodes = new AnimeEpisodeRepository();
            return repEpisodes.GetBySeriesID(session, AnimeSeriesID);
        }

        public int GetAnimeEpisodesNormalCountWithVideoLocal()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return
                    Convert.ToInt32(
                        session.CreateQuery(
                            "Select count(*) FROM AnimeEpisode as aepi, AniDB_Episode as epi WHERE aepi.AniDB_EpisodeID = epi.EpisodeID AND epi.EpisodeType=1 AND (select count(*) from VideoLocal as vl, CrossRef_File_Episode as xref where vl.Hash = xref.Hash and xref.EpisodeID = epi.EpisodeID) > 0 AND aepi.AnimeSeriesID = :animeid")
                            .SetParameter("animeid", AnimeSeriesID)
                            .UniqueResult());
            }
        }

        public int GetAnimeNumberOfEpisodeTypes()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return
                    Convert.ToInt32(
                        session.CreateQuery(
                            "Select count(distinct epi.EpisodeType) FROM AnimeEpisode as aepi, AniDB_Episode as epi WHERE aepi.AniDB_EpisodeID = epi.EpisodeID AND epi.EpisodeType=1 AND (select count(*) from VideoLocal as vl, CrossRef_File_Episode as xref where vl.Hash = xref.Hash and xref.EpisodeID = epi.EpisodeID) > 0 AND aepi.AnimeSeriesID = :animeid")
                            .SetParameter("animeid", AnimeSeriesID)
                            .UniqueResult());
            }
        }

        public int GetAnimeEpisodesCountWithVideoLocal()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return
                    Convert.ToInt32(
                        session.CreateQuery(
                            "Select count(*) FROM AnimeEpisode as aepi, AniDB_Episode as epi WHERE aepi.AniDB_EpisodeID = epi.EpisodeID AND (select count(*) from VideoLocal as vl, CrossRef_File_Episode as xref where vl.Hash = xref.Hash and xref.EpisodeID = epi.EpisodeID) > 0 AND aepi.AnimeSeriesID = :animeid")
                            .SetParameter("animeid", AnimeSeriesID)
                            .UniqueResult());
            }
        }

        public AnimeEpisode GetLastEpisodeWatched(int userID)
        {
            AnimeEpisode watchedep = null;
            AnimeEpisode_User userRecordWatched = null;

            foreach (var ep in GetAnimeEpisodes())
            {
                var userRecord = ep.GetUserRecord(userID);
                if (userRecord != null && ep.EpisodeTypeEnum == enEpisodeType.Episode)
                {
                    if (watchedep == null)
                    {
                        watchedep = ep;
                        userRecordWatched = userRecord;
                    }

                    if (userRecord.WatchedDate > userRecordWatched.WatchedDate)
                    {
                        watchedep = ep;
                        userRecordWatched = userRecord;
                    }
                }
            }
            return watchedep;
        }

        public AnimeSeries_User GetUserRecord(int userID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetUserRecord(session, userID);
            }
        }

        public AnimeSeries_User GetUserRecord(ISession session, int userID)
        {
            var repUser = new AnimeSeries_UserRepository();
            return repUser.GetByUserAndSeriesID(session, userID, AnimeSeriesID);
        }

        public AniDB_Anime GetAnime()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetAnime(session);
            }
        }

        public AniDB_Anime GetAnime(ISession session)
        {
            var repAnime = new AniDB_AnimeRepository();
            var anidb_anime = repAnime.GetByAnimeID(session, AniDB_ID);
            return anidb_anime;
        }

        public void Populate(AniDB_Anime anime)
        {
            AniDB_ID = anime.AnimeID;
            LatestLocalEpisodeNumber = 0;
            DateTimeUpdated = DateTime.Now;
            DateTimeCreated = DateTime.Now;
            SeriesNameOverride = "";
        }

        public void CreateAnimeEpisodes()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                CreateAnimeEpisodes(session);
            }
        }

        public void CreateAnimeEpisodes(ISession session)
        {
            var anime = GetAnime(session);
            if (anime == null) return;

            foreach (var ep in anime.GetAniDBEpisodes(session))
            {
                ep.CreateAnimeEpisode(session, AnimeSeriesID);
            }
        }

        public Contract_AnimeSeries ToContract(AnimeSeries_User userRecord, bool forceimages = false)
        {
            var anime = GetAnime();
            var tvDBCrossRefs = GetCrossRefTvDBV2();
            var movieDBCrossRef = CrossRefMovieDB;
            var malDBCrossRef = CrossRefMAL;
            MovieDB_Movie movie = null;
            if (movieDBCrossRef != null)
                movie = movieDBCrossRef.GetMovieDB_Movie();
            var sers = new List<TvDB_Series>();
            foreach (var xref in tvDBCrossRefs)
            {
                var tvser = xref.GetTvDBSeries();
                if (tvser != null)
                    sers.Add(tvser);
                else
                    logger.Warn("You are missing database information for TvDB series: {0} - {1}", xref.TvDBID,
                        xref.TvDBTitle);
            }

            return ToContract(anime, tvDBCrossRefs, movieDBCrossRef, userRecord, sers, malDBCrossRef, false, null, null,
                null, null, movie, forceimages);
        }

        public Contract_AnimeSeries ToContract(AniDB_Anime animeRec, List<CrossRef_AniDB_TvDBV2> tvDBCrossRefs,
            CrossRef_AniDB_Other movieDBCrossRef,
            AnimeSeries_User userRecord, List<TvDB_Series> tvseries, List<CrossRef_AniDB_MAL> malDBCrossRef,
            bool passedDefaultImages, AniDB_Anime_DefaultImage defPoster,
            AniDB_Anime_DefaultImage defFanart, AniDB_Anime_DefaultImage defWideBanner, List<AniDB_Anime_Title> titles,
            MovieDB_Movie movie, bool forceimages = false)
        {
            var contract = new Contract_AnimeSeries();

            contract.AniDB_ID = AniDB_ID;
            contract.AnimeGroupID = AnimeGroupID;
            contract.AnimeSeriesID = AnimeSeriesID;
            contract.DateTimeUpdated = DateTimeUpdated;
            contract.DateTimeCreated = DateTimeCreated;
            contract.DefaultAudioLanguage = DefaultAudioLanguage;
            contract.DefaultSubtitleLanguage = DefaultSubtitleLanguage;
            contract.LatestLocalEpisodeNumber = LatestLocalEpisodeNumber;
            contract.EpisodeAddedDate = EpisodeAddedDate;
            contract.MissingEpisodeCount = MissingEpisodeCount;
            contract.MissingEpisodeCountGroups = MissingEpisodeCountGroups;
            contract.SeriesNameOverride = SeriesNameOverride;
            contract.DefaultFolder = DefaultFolder;


            if (userRecord == null)
            {
                contract.PlayedCount = 0;
                contract.StoppedCount = 0;
                contract.UnwatchedEpisodeCount = 0;
                contract.WatchedCount = 0;
                contract.WatchedDate = null;
                contract.WatchedEpisodeCount = 0;
            }
            else
            {
                contract.PlayedCount = userRecord.PlayedCount;
                contract.StoppedCount = userRecord.StoppedCount;
                contract.UnwatchedEpisodeCount = userRecord.UnwatchedEpisodeCount;
                contract.WatchedCount = userRecord.WatchedCount;
                contract.WatchedDate = userRecord.WatchedDate;
                contract.WatchedEpisodeCount = userRecord.WatchedEpisodeCount;
            }

            // get AniDB data
            contract.AniDBAnime = null;
            if (animeRec != null)
            {
                var animecontract = animeRec.ToContract(false, titles);

                AniDB_Anime_DefaultImage defaultPoster = null;
                if (passedDefaultImages)
                    defaultPoster = defPoster;
                else
                    defaultPoster = animeRec.GetDefaultPoster();

                if (defaultPoster == null)
                    animecontract.DefaultImagePoster = null;
                else
                    animecontract.DefaultImagePoster = defaultPoster.ToContract();

                if ((animecontract.DefaultImagePoster == null) && forceimages)
                {
                    var im = animeRec.GetDefaultPosterDetailsNoBlanks();
                    if (im != null)
                    {
                        animecontract.DefaultImagePoster = new Contract_AniDB_Anime_DefaultImage();
                        animecontract.DefaultImagePoster.AnimeID = im.ImageID;
                        animecontract.DefaultImagePoster.ImageType = (int)im.ImageType;
                    }
                }
                AniDB_Anime_DefaultImage defaultFanart = null;
                if (passedDefaultImages)
                    defaultFanart = defFanart;
                else
                    defaultFanart = animeRec.GetDefaultFanart();

                if (defaultFanart == null)
                    animecontract.DefaultImageFanart = null;
                else
                    animecontract.DefaultImageFanart = defaultFanart.ToContract();

                if ((animecontract.DefaultImageFanart == null) && forceimages)
                {
                    var im = animeRec.GetDefaultFanartDetailsNoBlanks();
                    if (im != null)
                    {
                        animecontract.DefaultImageFanart = new Contract_AniDB_Anime_DefaultImage();
                        animecontract.DefaultImageFanart.AnimeID = im.ImageID;
                        animecontract.DefaultImageFanart.ImageType = (int)im.ImageType;
                    }
                }

                AniDB_Anime_DefaultImage defaultWideBanner = null;
                if (passedDefaultImages)
                    defaultWideBanner = defWideBanner;
                else
                    defaultWideBanner = animeRec.GetDefaultWideBanner();

                if (defaultWideBanner == null)
                    animecontract.DefaultImageWideBanner = null;
                else
                    animecontract.DefaultImageWideBanner = defaultWideBanner.ToContract();

                contract.AniDBAnime = animecontract;
            }

            contract.CrossRefAniDBTvDBV2 = new List<Contract_CrossRef_AniDB_TvDBV2>();
            foreach (var tvXref in tvDBCrossRefs)
                contract.CrossRefAniDBTvDBV2.Add(tvXref.ToContract());


            contract.TvDB_Series = new List<Contract_TvDB_Series>();
            foreach (var ser in tvseries)
                contract.TvDB_Series.Add(ser.ToContract());

            contract.CrossRefAniDBMovieDB = null;
            if (movieDBCrossRef != null)
            {
                contract.CrossRefAniDBMovieDB = movieDBCrossRef.ToContract();
                contract.MovieDB_Movie = movie.ToContract();
            }
            contract.CrossRefAniDBMAL = new List<Contract_CrossRef_AniDB_MAL>();
            if (malDBCrossRef != null)
            {
                foreach (var xref in malDBCrossRef)
                    contract.CrossRefAniDBMAL.Add(xref.ToContract());
            }

            return contract;
        }

        public override string ToString()
        {
            return string.Format("Series: {0} ({1})", GetAnime().MainTitle, AnimeSeriesID);
            //return "";
        }

        public void QueueUpdateStats()
        {
            var cmdRefreshAnime = new CommandRequest_RefreshAnime(AniDB_ID);
            cmdRefreshAnime.Save();
        }

        public void UpdateStats(bool watchedStats, bool missingEpsStats, bool updateAllGroupsAbove)
        {
            var start = DateTime.Now;
            var startOverall = DateTime.Now;
            logger.Info("Starting Updating STATS for SERIES {0} ({1} - {2} - {3})", ToString(), watchedStats,
                missingEpsStats, updateAllGroupsAbove);

            var repSeriesUser = new AnimeSeries_UserRepository();
            var repEpisodeUser = new AnimeEpisode_UserRepository();
            var repVids = new VideoLocalRepository();
            var repXrefs = new CrossRef_File_EpisodeRepository();

            var repUsers = new JMMUserRepository();
            var allUsers = repUsers.GetAll();

            var startEps = DateTime.Now;
            var eps = GetAnimeEpisodes();
            var tsEps = DateTime.Now - startEps;
            logger.Trace("Got episodes for SERIES {0} in {1}ms", ToString(), tsEps.TotalMilliseconds);

            var startVids = DateTime.Now;
            var vidsTemp = repVids.GetByAniDBAnimeID(AniDB_ID);
            var crossRefs = repXrefs.GetByAnimeID(AniDB_ID);

            var dictCrossRefs =
                new Dictionary<int, List<CrossRef_File_Episode>>();
            foreach (var xref in crossRefs)
            {
                if (!dictCrossRefs.ContainsKey(xref.EpisodeID))
                    dictCrossRefs[xref.EpisodeID] = new List<CrossRef_File_Episode>();
                dictCrossRefs[xref.EpisodeID].Add(xref);
            }

            var dictVids = new Dictionary<string, VideoLocal>();
            foreach (var vid in vidsTemp)
                dictVids[vid.Hash] = vid;

            var tsVids = DateTime.Now - startVids;
            logger.Trace("Got video locals for SERIES {0} in {1}ms", ToString(), tsVids.TotalMilliseconds);


            if (watchedStats)
            {
                foreach (var juser in allUsers)
                {
                    //this.WatchedCount = 0;
                    var userRecord = GetUserRecord(juser.JMMUserID);
                    if (userRecord == null) userRecord = new AnimeSeries_User(juser.JMMUserID, AnimeSeriesID);

                    // reset stats
                    userRecord.UnwatchedEpisodeCount = 0;
                    userRecord.WatchedEpisodeCount = 0;
                    userRecord.WatchedCount = 0;
                    userRecord.WatchedDate = null;

                    var startUser = DateTime.Now;
                    var epUserRecords = repEpisodeUser.GetByUserID(juser.JMMUserID);
                    var dictUserRecords = new Dictionary<int, AnimeEpisode_User>();
                    foreach (var usrec in epUserRecords)
                        dictUserRecords[usrec.AnimeEpisodeID] = usrec;
                    var tsUser = DateTime.Now - startUser;
                    logger.Trace("Got user records for SERIES {0}/{1} in {2}ms", ToString(), juser.Username,
                        tsUser.TotalMilliseconds);

                    foreach (var ep in eps)
                    {
                        // if the episode doesn't have any files then it won't count towards watched/unwatched counts
                        var epVids = new List<VideoLocal>();

                        if (dictCrossRefs.ContainsKey(ep.AniDB_EpisodeID))
                        {
                            foreach (var xref in dictCrossRefs[ep.AniDB_EpisodeID])
                            {
                                if (xref.EpisodeID == ep.AniDB_EpisodeID)
                                {
                                    if (dictVids.ContainsKey(xref.Hash))
                                        epVids.Add(dictVids[xref.Hash]);
                                }
                            }
                        }
                        if (epVids.Count == 0) continue;

                        if (ep.EpisodeTypeEnum == enEpisodeType.Episode ||
                            ep.EpisodeTypeEnum == enEpisodeType.Special)
                        {
                            AnimeEpisode_User epUserRecord = null;
                            if (dictUserRecords.ContainsKey(ep.AnimeEpisodeID))
                                epUserRecord = dictUserRecords[ep.AnimeEpisodeID];

                            if (epUserRecord != null && epUserRecord.WatchedDate.HasValue)
                                userRecord.WatchedEpisodeCount++;
                            else userRecord.UnwatchedEpisodeCount++;

                            if (epUserRecord != null)
                            {
                                if (userRecord.WatchedDate.HasValue)
                                {
                                    if (epUserRecord.WatchedDate > userRecord.WatchedDate)
                                        userRecord.WatchedDate = epUserRecord.WatchedDate;
                                }
                                else
                                    userRecord.WatchedDate = epUserRecord.WatchedDate;

                                userRecord.WatchedCount += epUserRecord.WatchedCount;
                            }
                        }
                    }
                    repSeriesUser.Save(userRecord);
                }
            }

            var ts = DateTime.Now - start;
            logger.Trace("Updated WATCHED stats for SERIES {0} in {1}ms", ToString(), ts.TotalMilliseconds);
            start = DateTime.Now;


            if (missingEpsStats)
            {
                var animeType = enAnimeType.TVSeries;
                var aniDB_Anime = GetAnime();
                if (aniDB_Anime != null)
                {
                    animeType = aniDB_Anime.AnimeTypeEnum;
                }

                MissingEpisodeCount = 0;
                MissingEpisodeCountGroups = 0;

                // get all the group status records
                var repGrpStat = new AniDB_GroupStatusRepository();
                var grpStatuses = repGrpStat.GetByAnimeID(AniDB_ID);

                // find all the episodes for which the user has a file
                // from this we can determine what their latest episode number is
                // find out which groups the user is collecting

                var userReleaseGroups = new List<int>();
                foreach (var ep in eps)
                {
                    var vids = new List<VideoLocal>();
                    if (dictCrossRefs.ContainsKey(ep.AniDB_EpisodeID))
                    {
                        foreach (var xref in dictCrossRefs[ep.AniDB_EpisodeID])
                        {
                            if (xref.EpisodeID == ep.AniDB_EpisodeID)
                            {
                                if (dictVids.ContainsKey(xref.Hash))
                                    vids.Add(dictVids[xref.Hash]);
                            }
                        }
                    }

                    //List<VideoLocal> vids = ep.VideoLocals;
                    foreach (var vid in vids)
                    {
                        var anifile = vid.GetAniDBFile();
                        if (anifile != null)
                        {
                            if (!userReleaseGroups.Contains(anifile.GroupID)) userReleaseGroups.Add(anifile.GroupID);
                        }
                    }
                }

                var latestLocalEpNumber = 0;
                var epReleasedList = new EpisodeList(animeType);
                var epGroupReleasedList = new EpisodeList(animeType);

                foreach (var ep in eps)
                {
                    //List<VideoLocal> vids = ep.VideoLocals;
                    if (ep.EpisodeTypeEnum != enEpisodeType.Episode) continue;

                    var vids = new List<VideoLocal>();
                    if (dictCrossRefs.ContainsKey(ep.AniDB_EpisodeID))
                    {
                        foreach (var xref in dictCrossRefs[ep.AniDB_EpisodeID])
                        {
                            if (xref.EpisodeID == ep.AniDB_EpisodeID)
                            {
                                if (dictVids.ContainsKey(xref.Hash))
                                    vids.Add(dictVids[xref.Hash]);
                            }
                        }
                    }


                    var aniEp = ep.AniDB_Episode;
                    var thisEpNum = aniEp.EpisodeNumber;

                    if (thisEpNum > latestLocalEpNumber && vids.Count > 0)
                        latestLocalEpNumber = thisEpNum;

                    // does this episode have a file released 
                    // does this episode have a file released by the group the user is collecting
                    var epReleased = false;
                    var epReleasedGroup = false;
                    foreach (var gs in grpStatuses)
                    {
                        if (gs.LastEpisodeNumber >= thisEpNum) epReleased = true;
                        if (userReleaseGroups.Contains(gs.GroupID) && gs.HasGroupReleasedEpisode(thisEpNum))
                            epReleasedGroup = true;
                    }


                    try
                    {
                        epReleasedList.Add(ep, !epReleased || vids.Count != 0);
                        epGroupReleasedList.Add(ep, !epReleasedGroup || vids.Count != 0);
                    }
                    catch (Exception e)
                    {
                        logger.Trace("Error {0}", e.ToString());
                        throw;
                    }
                }
                foreach (var eplst in epReleasedList)
                {
                    if (!eplst.Available)
                        MissingEpisodeCount++;
                }
                foreach (var eplst in epGroupReleasedList)
                {
                    if (!eplst.Available)
                        MissingEpisodeCountGroups++;
                }

                LatestLocalEpisodeNumber = latestLocalEpNumber;
            }

            ts = DateTime.Now - start;
            logger.Trace("Updated MISSING EPS stats for SERIES {0} in {1}ms", ToString(), ts.TotalMilliseconds);
            start = DateTime.Now;

            var rep = new AnimeSeriesRepository();
            rep.Save(this);

            if (updateAllGroupsAbove)
            {
                foreach (var grp in AllGroupsAbove)
                {
                    grp.UpdateStats(watchedStats, missingEpsStats);
                }
            }

            ts = DateTime.Now - start;
            logger.Trace("Updated GROUPS ABOVE stats for SERIES {0} in {1}ms", ToString(), ts.TotalMilliseconds);
            start = DateTime.Now;

            var tsOverall = DateTime.Now - startOverall;
            logger.Info("Finished Updating STATS for SERIES {0} in {1}ms ({2} - {3} - {4})", ToString(),
                tsOverall.TotalMilliseconds,
                watchedStats, missingEpsStats, updateAllGroupsAbove);

            StatsCache.Instance.UpdateUsingSeries(AnimeSeriesID);
        }

        internal class EpisodeList : List<EpisodeList.StatEpisodes>
        {
            private readonly Regex partmatch = new Regex("part (\\d.*?) of (\\d.*)");
            private readonly Regex remmultispace = new Regex("\\s+");
            private readonly Regex remsymbols = new Regex("[^A-Za-z0-9 ]");

            public EpisodeList(enAnimeType ept)
            {
                AnimeType = ept;
            }

            private enAnimeType AnimeType { get; }

            public void Add(AnimeEpisode ep, bool available)
            {
                if ((AnimeType == enAnimeType.OVA) || (AnimeType == enAnimeType.Movie))
                {
                    var aniEp = ep.AniDB_Episode;
                    var ename = aniEp.EnglishName.ToLower();
                    var m = partmatch.Match(ename);
                    var s = new StatEpisodes.StatEpisode();
                    s.Available = available;
                    if (m.Success)
                    {
                        var part_number = 0;
                        var part_count = 0;
                        int.TryParse(m.Groups[1].Value, out part_number);
                        int.TryParse(m.Groups[2].Value, out part_count);
                        var rname = partmatch.Replace(ename, string.Empty);
                        rname = remsymbols.Replace(rname, string.Empty);
                        rname = remmultispace.Replace(rname, " ");


                        s.EpisodeType = StatEpisodes.StatEpisode.EpType.Part;
                        s.PartCount = part_count;
                        s.Match = rname.Trim();
                        if ((s.Match == "complete movie") || (s.Match == "movie") || (s.Match == "ova"))
                            s.Match = string.Empty;
                    }
                    else
                    {
                        if ((ename == "complete movie") || (ename == "movie") || (ename == "ova"))
                        {
                            s.Match = string.Empty;
                        }
                        else
                        {
                            var rname = partmatch.Replace(aniEp.EnglishName.ToLower(), string.Empty);
                            rname = remsymbols.Replace(rname, string.Empty);
                            rname = remmultispace.Replace(rname, " ");
                            s.Match = rname.Trim();
                        }
                        s.EpisodeType = StatEpisodes.StatEpisode.EpType.Complete;
                        s.PartCount = 0;
                    }
                    StatEpisodes fnd = null;
                    foreach (var k in this)
                    {
                        foreach (var ss in k)
                        {
                            if (ss.Match == s.Match)
                            {
                                fnd = k;
                                break;
                            }
                        }
                        if (fnd != null)
                            break;
                    }
                    if (fnd == null)
                    {
                        var eps = new StatEpisodes();
                        eps.Add(s);
                        Add(eps);
                    }
                    else
                        fnd.Add(s);
                }
                else
                {
                    var eps = new StatEpisodes();
                    var es = new StatEpisodes.StatEpisode();
                    es.Match = string.Empty;
                    es.EpisodeType = StatEpisodes.StatEpisode.EpType.Complete;
                    es.PartCount = 0;
                    es.Available = available;
                    eps.Add(es);
                    Add(eps);
                }
            }

            public class StatEpisodes : List<StatEpisodes.StatEpisode>
            {
                public bool Available
                {
                    get
                    {
                        var maxcnt = 0;
                        foreach (var k in this)
                        {
                            if (k.PartCount > maxcnt)
                                maxcnt = k.PartCount;
                        }
                        var parts = new int[maxcnt + 1];
                        foreach (var k in this)
                        {
                            if ((k.EpisodeType == StatEpisode.EpType.Complete) && k.Available)
                                return true;
                            if ((k.EpisodeType == StatEpisode.EpType.Part) && k.Available)
                            {
                                parts[k.PartCount]++;
                                if (parts[k.PartCount] == k.PartCount)
                                    return true;
                            }
                        }
                        return false;
                    }
                }

                public class StatEpisode
                {
                    public enum EpType
                    {
                        Complete,
                        Part
                    }

                    public string Match;
                    public int PartCount;
                    public EpType EpisodeType { get; set; }
                    public bool Available { get; set; }
                }
            }
        }

        #region DB Columns

        public int AnimeSeriesID { get; private set; }
        public int AnimeGroupID { get; set; }
        public int AniDB_ID { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public DateTime DateTimeCreated { get; set; }
        public string DefaultAudioLanguage { get; set; }
        public string DefaultSubtitleLanguage { get; set; }
        public DateTime? EpisodeAddedDate { get; set; }
        public int MissingEpisodeCount { get; set; }
        public int MissingEpisodeCountGroups { get; set; }
        public int LatestLocalEpisodeNumber { get; set; }
        public string SeriesNameOverride { get; set; }

        public string DefaultFolder { get; set; }

        #endregion

        #region TvDB

        public List<CrossRef_AniDB_TvDBV2> GetCrossRefTvDBV2()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetCrossRefTvDBV2(session);
            }
        }

        public List<CrossRef_AniDB_TvDBV2> GetCrossRefTvDBV2(ISession session)
        {
            var repCrossRef = new CrossRef_AniDB_TvDBV2Repository();
            return repCrossRef.GetByAnimeID(session, AniDB_ID);
        }

        public List<TvDB_Series> GetTvDBSeries()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetTvDBSeries(session);
            }
        }

        public List<TvDB_Series> GetTvDBSeries(ISession session)
        {
            var sers = new List<TvDB_Series>();

            var xrefs = GetCrossRefTvDBV2(session);
            if (xrefs == null || xrefs.Count == 0) return sers;

            foreach (var xref in xrefs)
                sers.Add(xref.GetTvDBSeries(session));

            return sers;
        }

        #endregion

        #region Trakt

        public List<CrossRef_AniDB_TraktV2> GetCrossRefTraktV2()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetCrossRefTraktV2(session);
            }
        }

        public List<CrossRef_AniDB_TraktV2> GetCrossRefTraktV2(ISession session)
        {
            var repCrossRef = new CrossRef_AniDB_TraktV2Repository();
            return repCrossRef.GetByAnimeID(session, AniDB_ID);
        }

        public List<Trakt_Show> GetTraktShow()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetTraktShow(session);
            }
        }

        public List<Trakt_Show> GetTraktShow(ISession session)
        {
            var sers = new List<Trakt_Show>();

            var xrefs = GetCrossRefTraktV2(session);
            if (xrefs == null || xrefs.Count == 0) return sers;

            foreach (var xref in xrefs)
                sers.Add(xref.GetByTraktShow(session));

            return sers;
        }

        #endregion
    }
}