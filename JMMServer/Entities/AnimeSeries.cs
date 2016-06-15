using System;
using System.Collections.Generic;
using JMMContracts;
using JMMServer.Commands;
using JMMServer.ImageDownload;
using JMMServer.Repositories;
using NHibernate;
using NLog;

namespace JMMServer.Entities
{
    public class AnimeSeries
    {
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

        public string Year
        {
            get { return GetAnime().Year; }
        }

        private static Logger logger = LogManager.GetCurrentClassLogger();

        public string GetSeriesName()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetSeriesName(session);
            }
        }

        public string GetSeriesName(ISession session)
        {
            string seriesName = "";
            if (!string.IsNullOrEmpty(SeriesNameOverride))
                seriesName = SeriesNameOverride;
            else
            {
                if (ServerSettings.SeriesNameSource == DataSourceType.AniDB)
                    seriesName = GetAnime(session).GetFormattedTitle();
                else
                {
                    List<TvDB_Series> tvdbs = this.GetTvDBSeries(session);

                    if (tvdbs != null && tvdbs.Count > 0 && !string.IsNullOrEmpty(tvdbs[0].SeriesName) &&
                        !tvdbs[0].SeriesName.ToUpper().Contains("**DUPLICATE"))
                        seriesName = tvdbs[0].SeriesName;
                    else
                        seriesName = GetAnime(session).GetFormattedTitle(session);
                }
            }

            return seriesName;
        }

        public string GenresRaw
        {
            get
            {
                if (GetAnime() == null)
                    return "";
                else
                    return GetAnime().TagsString;
            }
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
            AnimeEpisodeRepository repEpisodes = new AnimeEpisodeRepository();
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
            CrossRef_AniDB_TvDBV2Repository repCrossRef = new CrossRef_AniDB_TvDBV2Repository();
            return repCrossRef.GetByAnimeID(session, this.AniDB_ID);
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
            List<TvDB_Series> sers = new List<TvDB_Series>();

            List<CrossRef_AniDB_TvDBV2> xrefs = GetCrossRefTvDBV2(session);
            if (xrefs == null || xrefs.Count == 0) return sers;

            foreach (CrossRef_AniDB_TvDBV2 xref in xrefs)
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
            CrossRef_AniDB_TraktV2Repository repCrossRef = new CrossRef_AniDB_TraktV2Repository();
            return repCrossRef.GetByAnimeID(session, this.AniDB_ID);
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
            List<Trakt_Show> sers = new List<Trakt_Show>();

            List<CrossRef_AniDB_TraktV2> xrefs = GetCrossRefTraktV2(session);
            if (xrefs == null || xrefs.Count == 0) return sers;

            foreach (CrossRef_AniDB_TraktV2 xref in xrefs)
                sers.Add(xref.GetByTraktShow(session));

            return sers;
        }

        #endregion

        public CrossRef_AniDB_Other CrossRefMovieDB
        {
            get
            {
                CrossRef_AniDB_OtherRepository repCrossRef = new CrossRef_AniDB_OtherRepository();
                return repCrossRef.GetByAnimeIDAndType(this.AniDB_ID, CrossRefType.MovieDB);
            }
        }

        public List<CrossRef_AniDB_MAL> CrossRefMAL
        {
            get
            {
                CrossRef_AniDB_MALRepository repCrossRef = new CrossRef_AniDB_MALRepository();
                return repCrossRef.GetByAnimeID(this.AniDB_ID);
            }
        }

        public AnimeEpisode GetLastEpisodeWatched(int userID)
        {
            AnimeEpisode watchedep = null;
            AnimeEpisode_User userRecordWatched = null;

            foreach (AnimeEpisode ep in GetAnimeEpisodes())
            {
                AnimeEpisode_User userRecord = ep.GetUserRecord(userID);
                if (userRecord != null && ep.EpisodeTypeEnum == AniDBAPI.enEpisodeType.Episode)
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
            AnimeSeries_UserRepository repUser = new AnimeSeries_UserRepository();
            return repUser.GetByUserAndSeriesID(session, userID, this.AnimeSeriesID);
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
            AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
            AniDB_Anime anidb_anime = repAnime.GetByAnimeID(session, this.AniDB_ID);
            return anidb_anime;
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

        public void Populate(AniDB_Anime anime)
        {
            this.AniDB_ID = anime.AnimeID;
            this.LatestLocalEpisodeNumber = 0;
            this.DateTimeUpdated = DateTime.Now;
            this.DateTimeCreated = DateTime.Now;
            this.SeriesNameOverride = "";
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
            AniDB_Anime anime = GetAnime(session);
            if (anime == null) return;

            foreach (AniDB_Episode ep in anime.GetAniDBEpisodes(session))
            {
                ep.CreateAnimeEpisode(session, this.AnimeSeriesID);
            }
        }

        /// <summary>
        /// Gets the direct parent AnimeGroup this series belongs to
        /// </summary>
        public AnimeGroup AnimeGroup
        {
            get
            {
                AnimeGroupRepository repGroups = new AnimeGroupRepository();
                return repGroups.GetByID(this.AnimeGroupID);
            }
        }

        /// <summary>
        /// Gets the very top level AnimeGroup which this series belongs to
        /// </summary>
        public AnimeGroup TopLevelAnimeGroup
        {
            get
            {
                AnimeGroupRepository repGroups = new AnimeGroupRepository();
                AnimeGroup parentGroup = repGroups.GetByID(this.AnimeGroupID);

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
                List<AnimeGroup> grps = new List<AnimeGroup>();
                try
                {
                    AnimeGroupRepository repGroups = new AnimeGroupRepository();
                    AnimeSeriesRepository repSeries = new AnimeSeriesRepository();

                    int? groupID = AnimeGroupID;
                    while (groupID.HasValue)
                    {
                        AnimeGroup grp = repGroups.GetByID(groupID.Value);
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

        public Contract_AnimeSeries ToContract(AnimeSeries_User userRecord, bool forceimages = false)
        {
            AniDB_Anime anime = this.GetAnime();
            List<CrossRef_AniDB_TvDBV2> tvDBCrossRefs = this.GetCrossRefTvDBV2();
            CrossRef_AniDB_Other movieDBCrossRef = this.CrossRefMovieDB;
            List<CrossRef_AniDB_MAL> malDBCrossRef = this.CrossRefMAL;
            MovieDB_Movie movie = null;
            if (movieDBCrossRef != null)
                movie = movieDBCrossRef.GetMovieDB_Movie();
            List<TvDB_Series> sers = new List<TvDB_Series>();
            foreach (CrossRef_AniDB_TvDBV2 xref in tvDBCrossRefs)
            {
                TvDB_Series tvser = xref.GetTvDBSeries();
                if (tvser != null)
                    sers.Add(tvser);
                else
                    logger.Warn("You are missing database information for TvDB series: {0} - {1}", xref.TvDBID,
                        xref.TvDBTitle);
            }

            return this.ToContract(anime, tvDBCrossRefs, movieDBCrossRef, userRecord, sers, malDBCrossRef, false, null,
                null, null, null, movie, forceimages);
        }

        public Contract_AnimeSeries ToContract(AniDB_Anime animeRec, List<CrossRef_AniDB_TvDBV2> tvDBCrossRefs,
            CrossRef_AniDB_Other movieDBCrossRef,
            AnimeSeries_User userRecord, List<TvDB_Series> tvseries, List<CrossRef_AniDB_MAL> malDBCrossRef,
            bool passedDefaultImages, AniDB_Anime_DefaultImage defPoster,
            AniDB_Anime_DefaultImage defFanart, AniDB_Anime_DefaultImage defWideBanner, List<AniDB_Anime_Title> titles,
            MovieDB_Movie movie, bool forceimages = false)
        {
            Contract_AnimeSeries contract = new Contract_AnimeSeries();

            contract.AniDB_ID = this.AniDB_ID;
            contract.AnimeGroupID = this.AnimeGroupID;
            contract.AnimeSeriesID = this.AnimeSeriesID;
            contract.DateTimeUpdated = this.DateTimeUpdated;
            contract.DateTimeCreated = this.DateTimeCreated;
            contract.DefaultAudioLanguage = this.DefaultAudioLanguage;
            contract.DefaultSubtitleLanguage = this.DefaultSubtitleLanguage;
            contract.LatestLocalEpisodeNumber = this.LatestLocalEpisodeNumber;
            contract.EpisodeAddedDate = this.EpisodeAddedDate;
            contract.MissingEpisodeCount = this.MissingEpisodeCount;
            contract.MissingEpisodeCountGroups = this.MissingEpisodeCountGroups;
            contract.SeriesNameOverride = this.SeriesNameOverride;
            contract.DefaultFolder = this.DefaultFolder;


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
                Contract_AniDBAnime animecontract = animeRec.ToContract(false, titles);

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
                    ImageDetails im = animeRec.GetDefaultPosterDetailsNoBlanks();
                    if (im != null)
                    {
                        animecontract.DefaultImagePoster = new Contract_AniDB_Anime_DefaultImage();
                        animecontract.DefaultImagePoster.AnimeID = im.ImageID;
                        animecontract.DefaultImagePoster.ImageType = (int) im.ImageType;
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
                    ImageDetails im = animeRec.GetDefaultFanartDetailsNoBlanks();
                    if (im != null)
                    {
                        animecontract.DefaultImageFanart = new Contract_AniDB_Anime_DefaultImage();
                        animecontract.DefaultImageFanart.AnimeID = im.ImageID;
                        animecontract.DefaultImageFanart.ImageType = (int) im.ImageType;
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
            foreach (CrossRef_AniDB_TvDBV2 tvXref in tvDBCrossRefs)
                contract.CrossRefAniDBTvDBV2.Add(tvXref.ToContract());


            contract.TvDB_Series = new List<Contract_TvDB_Series>();
            foreach (TvDB_Series ser in tvseries)
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
                foreach (CrossRef_AniDB_MAL xref in malDBCrossRef)
                    contract.CrossRefAniDBMAL.Add(xref.ToContract());
            }

            return contract;
        }

        public override string ToString()
        {
            return string.Format("Series: {0} ({1})", GetAnime().MainTitle, AnimeSeriesID);
            //return "";
        }

        internal class EpisodeList : List<EpisodeList.StatEpisodes>
        {
            public EpisodeList(enAnimeType ept)
            {
                AnimeType = ept;
            }

            private enAnimeType AnimeType { get; set; }

            System.Text.RegularExpressions.Regex partmatch =
                new System.Text.RegularExpressions.Regex("part (\\d.*?) of (\\d.*)");

            System.Text.RegularExpressions.Regex remsymbols = new System.Text.RegularExpressions.Regex("[^A-Za-z0-9 ]");
            private System.Text.RegularExpressions.Regex remmultispace = new System.Text.RegularExpressions.Regex("\\s+");

            public void Add(AnimeEpisode ep, bool available)
            {
                if ((AnimeType == enAnimeType.OVA) || (AnimeType == enAnimeType.Movie))
                {
                    AniDB_Episode aniEp = ep.AniDB_Episode;
                    string ename = aniEp.EnglishName.ToLower();
                    System.Text.RegularExpressions.Match m = partmatch.Match(ename);
                    StatEpisodes.StatEpisode s = new StatEpisodes.StatEpisode();
                    s.Available = available;
                    if (m.Success)
                    {
                        int part_number = 0;
                        int part_count = 0;
                        int.TryParse(m.Groups[1].Value, out part_number);
                        int.TryParse(m.Groups[2].Value, out part_count);
                        string rname = partmatch.Replace(ename, string.Empty);
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
                            string rname = partmatch.Replace(aniEp.EnglishName.ToLower(), string.Empty);
                            rname = remsymbols.Replace(rname, string.Empty);
                            rname = remmultispace.Replace(rname, " ");
                            s.Match = rname.Trim();
                        }
                        s.EpisodeType = StatEpisodes.StatEpisode.EpType.Complete;
                        s.PartCount = 0;
                    }
                    StatEpisodes fnd = null;
                    foreach (StatEpisodes k in this)
                    {
                        foreach (StatEpisodes.StatEpisode ss in k)
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
                        StatEpisodes eps = new StatEpisodes();
                        eps.Add(s);
                        Add(eps);
                    }
                    else
                        fnd.Add(s);
                }
                else
                {
                    StatEpisodes eps = new StatEpisodes();
                    StatEpisodes.StatEpisode es = new StatEpisodes.StatEpisode();
                    es.Match = string.Empty;
                    es.EpisodeType = StatEpisodes.StatEpisode.EpType.Complete;
                    es.PartCount = 0;
                    es.Available = available;
                    eps.Add(es);
                    this.Add(eps);
                }
            }

            public class StatEpisodes : List<StatEpisodes.StatEpisode>
            {
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

                public bool Available
                {
                    get
                    {
                        int maxcnt = 0;
                        foreach (StatEpisode k in this)
                        {
                            if (k.PartCount > maxcnt)
                                maxcnt = k.PartCount;
                        }
                        int[] parts = new int[maxcnt + 1];
                        foreach (StatEpisode k in this)
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
            }
        }

        public void QueueUpdateStats()
        {
            CommandRequest_RefreshAnime cmdRefreshAnime = new CommandRequest_RefreshAnime(this.AniDB_ID);
            cmdRefreshAnime.Save();
        }

        public void UpdateStats(bool watchedStats, bool missingEpsStats, bool updateAllGroupsAbove)
        {
            DateTime start = DateTime.Now;
            DateTime startOverall = DateTime.Now;
            logger.Info("Starting Updating STATS for SERIES {0} ({1} - {2} - {3})", this.ToString(), watchedStats,
                missingEpsStats, updateAllGroupsAbove);

            AnimeSeries_UserRepository repSeriesUser = new AnimeSeries_UserRepository();
            AnimeEpisode_UserRepository repEpisodeUser = new AnimeEpisode_UserRepository();
            VideoLocalRepository repVids = new VideoLocalRepository();
            CrossRef_File_EpisodeRepository repXrefs = new CrossRef_File_EpisodeRepository();

            JMMUserRepository repUsers = new JMMUserRepository();
            List<JMMUser> allUsers = repUsers.GetAll();

            DateTime startEps = DateTime.Now;
            List<AnimeEpisode> eps = GetAnimeEpisodes();
            TimeSpan tsEps = DateTime.Now - startEps;
            logger.Trace("Got episodes for SERIES {0} in {1}ms", this.ToString(), tsEps.TotalMilliseconds);

            DateTime startVids = DateTime.Now;
            List<VideoLocal> vidsTemp = repVids.GetByAniDBAnimeID(this.AniDB_ID);
            List<CrossRef_File_Episode> crossRefs = repXrefs.GetByAnimeID(this.AniDB_ID);

            Dictionary<int, List<CrossRef_File_Episode>> dictCrossRefs =
                new Dictionary<int, List<CrossRef_File_Episode>>();
            foreach (CrossRef_File_Episode xref in crossRefs)
            {
                if (!dictCrossRefs.ContainsKey(xref.EpisodeID))
                    dictCrossRefs[xref.EpisodeID] = new List<CrossRef_File_Episode>();
                dictCrossRefs[xref.EpisodeID].Add(xref);
            }

            Dictionary<string, VideoLocal> dictVids = new Dictionary<string, VideoLocal>();
            foreach (VideoLocal vid in vidsTemp)
                dictVids[vid.Hash] = vid;

            TimeSpan tsVids = DateTime.Now - startVids;
            logger.Trace("Got video locals for SERIES {0} in {1}ms", this.ToString(), tsVids.TotalMilliseconds);


            if (watchedStats)
            {
                foreach (JMMUser juser in allUsers)
                {
                    //this.WatchedCount = 0;
                    AnimeSeries_User userRecord = GetUserRecord(juser.JMMUserID);
                    if (userRecord == null) userRecord = new AnimeSeries_User(juser.JMMUserID, this.AnimeSeriesID);

                    // reset stats
                    userRecord.UnwatchedEpisodeCount = 0;
                    userRecord.WatchedEpisodeCount = 0;
                    userRecord.WatchedCount = 0;
                    userRecord.WatchedDate = null;

                    DateTime startUser = DateTime.Now;
                    List<AnimeEpisode_User> epUserRecords = repEpisodeUser.GetByUserID(juser.JMMUserID);
                    Dictionary<int, AnimeEpisode_User> dictUserRecords = new Dictionary<int, AnimeEpisode_User>();
                    foreach (AnimeEpisode_User usrec in epUserRecords)
                        dictUserRecords[usrec.AnimeEpisodeID] = usrec;
                    TimeSpan tsUser = DateTime.Now - startUser;
                    logger.Trace("Got user records for SERIES {0}/{1} in {2}ms", this.ToString(), juser.Username,
                        tsUser.TotalMilliseconds);

                    foreach (AnimeEpisode ep in eps)
                    {
                        // if the episode doesn't have any files then it won't count towards watched/unwatched counts
                        List<VideoLocal> epVids = new List<VideoLocal>();

                        if (dictCrossRefs.ContainsKey(ep.AniDB_EpisodeID))
                        {
                            foreach (CrossRef_File_Episode xref in dictCrossRefs[ep.AniDB_EpisodeID])
                            {
                                if (xref.EpisodeID == ep.AniDB_EpisodeID)
                                {
                                    if (dictVids.ContainsKey(xref.Hash))
                                        epVids.Add(dictVids[xref.Hash]);
                                }
                            }
                        }
                        if (epVids.Count == 0) continue;

                        if (ep.EpisodeTypeEnum == AniDBAPI.enEpisodeType.Episode ||
                            ep.EpisodeTypeEnum == AniDBAPI.enEpisodeType.Special)
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

            TimeSpan ts = DateTime.Now - start;
            logger.Trace("Updated WATCHED stats for SERIES {0} in {1}ms", this.ToString(), ts.TotalMilliseconds);
            start = DateTime.Now;


            if (missingEpsStats)
            {
                enAnimeType animeType = enAnimeType.TVSeries;
                AniDB_Anime aniDB_Anime = this.GetAnime();
                if (aniDB_Anime != null)
                {
                    animeType = aniDB_Anime.AnimeTypeEnum;
                }

                MissingEpisodeCount = 0;
                MissingEpisodeCountGroups = 0;

                // get all the group status records
                AniDB_GroupStatusRepository repGrpStat = new AniDB_GroupStatusRepository();
                List<AniDB_GroupStatus> grpStatuses = repGrpStat.GetByAnimeID(this.AniDB_ID);

                // find all the episodes for which the user has a file
                // from this we can determine what their latest episode number is
                // find out which groups the user is collecting

                List<int> userReleaseGroups = new List<int>();
                foreach (AnimeEpisode ep in eps)
                {
                    List<VideoLocal> vids = new List<VideoLocal>();
                    if (dictCrossRefs.ContainsKey(ep.AniDB_EpisodeID))
                    {
                        foreach (CrossRef_File_Episode xref in dictCrossRefs[ep.AniDB_EpisodeID])
                        {
                            if (xref.EpisodeID == ep.AniDB_EpisodeID)
                            {
                                if (dictVids.ContainsKey(xref.Hash))
                                    vids.Add(dictVids[xref.Hash]);
                            }
                        }
                    }

                    //List<VideoLocal> vids = ep.VideoLocals;
                    foreach (VideoLocal vid in vids)
                    {
                        AniDB_File anifile = vid.GetAniDBFile();
                        if (anifile != null)
                        {
                            if (!userReleaseGroups.Contains(anifile.GroupID)) userReleaseGroups.Add(anifile.GroupID);
                        }
                    }
                }

                int latestLocalEpNumber = 0;
                EpisodeList epReleasedList = new EpisodeList(animeType);
                EpisodeList epGroupReleasedList = new EpisodeList(animeType);

                foreach (AnimeEpisode ep in eps)
                {
                    //List<VideoLocal> vids = ep.VideoLocals;
                    if (ep.EpisodeTypeEnum != AniDBAPI.enEpisodeType.Episode) continue;

                    List<VideoLocal> vids = new List<VideoLocal>();
                    if (dictCrossRefs.ContainsKey(ep.AniDB_EpisodeID))
                    {
                        foreach (CrossRef_File_Episode xref in dictCrossRefs[ep.AniDB_EpisodeID])
                        {
                            if (xref.EpisodeID == ep.AniDB_EpisodeID)
                            {
                                if (dictVids.ContainsKey(xref.Hash))
                                    vids.Add(dictVids[xref.Hash]);
                            }
                        }
                    }


                    AniDB_Episode aniEp = ep.AniDB_Episode;
                    int thisEpNum = aniEp.EpisodeNumber;

                    if (thisEpNum > latestLocalEpNumber && vids.Count > 0)
                        latestLocalEpNumber = thisEpNum;

                    // does this episode have a file released 
                    // does this episode have a file released by the group the user is collecting
                    bool epReleased = false;
                    bool epReleasedGroup = false;
                    foreach (AniDB_GroupStatus gs in grpStatuses)
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
                foreach (EpisodeList.StatEpisodes eplst in epReleasedList)
                {
                    if (!eplst.Available)
                        MissingEpisodeCount++;
                }
                foreach (EpisodeList.StatEpisodes eplst in epGroupReleasedList)
                {
                    if (!eplst.Available)
                        MissingEpisodeCountGroups++;
                }

                this.LatestLocalEpisodeNumber = latestLocalEpNumber;
            }

            ts = DateTime.Now - start;
            logger.Trace("Updated MISSING EPS stats for SERIES {0} in {1}ms", this.ToString(), ts.TotalMilliseconds);
            start = DateTime.Now;

            AnimeSeriesRepository rep = new AnimeSeriesRepository();
            rep.Save(this);

            if (updateAllGroupsAbove)
            {
                foreach (AnimeGroup grp in AllGroupsAbove)
                {
                    grp.UpdateStats(watchedStats, missingEpsStats);
                }
            }

            ts = DateTime.Now - start;
            logger.Trace("Updated GROUPS ABOVE stats for SERIES {0} in {1}ms", this.ToString(), ts.TotalMilliseconds);
            start = DateTime.Now;

            TimeSpan tsOverall = DateTime.Now - startOverall;
            logger.Info("Finished Updating STATS for SERIES {0} in {1}ms ({2} - {3} - {4})", this.ToString(),
                tsOverall.TotalMilliseconds,
                watchedStats, missingEpsStats, updateAllGroupsAbove);

            StatsCache.Instance.UpdateUsingSeries(AnimeSeriesID);
        }
    }
}