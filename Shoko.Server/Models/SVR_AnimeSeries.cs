﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using NHibernate;
using NHibernate.Util;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Commons.Utils;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.LZ4;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Settings;

namespace Shoko.Server.Models
{
    public class SVR_AnimeSeries : AnimeSeries
    {
        #region DB Columns

        public int ContractVersion { get; set; }
        public byte[] ContractBlob { get; set; }
        public int ContractSize { get; set; }

        public DateTime UpdatedAt { get; set; }

        #endregion

        public const int CONTRACT_VERSION = 9;


        private CL_AnimeSeries_User _contract;

        public virtual CL_AnimeSeries_User Contract
        {
            get
            {
                if ((_contract == null) && (ContractBlob != null) && (ContractBlob.Length > 0) && (ContractSize > 0))
                    _contract = CompressionHelper.DeserializeObject<CL_AnimeSeries_User>(ContractBlob, ContractSize);

                return _contract;
            }
            set
            {
                _contract = value;
                ContractBlob = CompressionHelper.SerializeObject(value, out int outsize);
                ContractSize = outsize;
                ContractVersion = CONTRACT_VERSION;
            }
        }

        public void CollectContractMemory()
        {
            _contract = null;
        }


        public string Year => GetAnime().GetYear();

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public string GetSeriesName()
        {
            string seriesName;
            if (!string.IsNullOrEmpty(SeriesNameOverride))
                seriesName = SeriesNameOverride;
            else
            {
                if (ServerSettings.Instance.SeriesNameSource == DataSourceType.AniDB)
                    seriesName = GetAnime().PreferredTitle;
                else
                {
                    List<TvDB_Series> tvdbs = GetTvDBSeries();

                    if (tvdbs != null && tvdbs.Count > 0 && !string.IsNullOrEmpty(tvdbs[0].SeriesName) &&
                        !tvdbs[0].SeriesName.ToUpper().Contains("**DUPLICATE"))
                        seriesName = tvdbs[0].SeriesName;
                    else
                        seriesName = GetAnime().PreferredTitle;
                }
            }

            return seriesName;
        }

        public HashSet<string> GetAllTitles()
        {
            HashSet<string> titles = new HashSet<string>();
            
            // Override
            if (SeriesNameOverride != null) titles.Add(SeriesNameOverride);
            // AniDB
            if (GetAnime() != null) titles.UnionWith(GetAnime().GetAllTitles());
            else logger.Error($"A Series has a null AniDB_Anime. That is bad. The AniDB ID is {AniDB_ID}");
            // TvDB
            var tvdb = GetTvDBSeries();
            if (tvdb != null)
                titles.UnionWith(tvdb.Select(a => a?.SeriesName).Where(a => a != null));

            // MovieDB
            var movieDB = GetMovieDB();
            if (movieDB != null)
            {
                titles.Add(movieDB.MovieName);
                titles.Add(movieDB.OriginalName);
            }

            return titles;
        }

        public string GenresRaw
        {
            get
            {
                if (GetAnime() == null)
                    return string.Empty;
                return GetAnime().TagsString;
            }
        }


        public List<SVR_AnimeEpisode> GetAnimeEpisodes()
        {
            return RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID);
        }

        public int GetAnimeEpisodesCountWithVideoLocal()
        {
            return RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID).Count(a => a.GetVideoLocals().Any());
        }

        public int GetAnimeEpisodesNormalCountWithVideoLocal()
        {
            return RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID).Count(a =>
                a.AniDB_Episode != null && a.GetVideoLocals().Any() && a.EpisodeTypeEnum == EpisodeType.Episode);
        }

        public int GetAnimeEpisodesAndSpecialsCountWithVideoLocal()
        {
            return RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID).Count(a =>
                a.AniDB_Episode != null && a.GetVideoLocals().Any() &&
                (a.EpisodeTypeEnum == EpisodeType.Episode || a.EpisodeTypeEnum == EpisodeType.Special));
        }

        public int GetAnimeNumberOfEpisodeTypes()
        {
            return RepoFactory.AnimeEpisode
                .GetBySeriesID(AnimeSeriesID)
                .Where(a => a.AniDB_Episode != null && RepoFactory.CrossRef_File_Episode
                                .GetByEpisodeID(
                                    RepoFactory.AniDB_Episode.GetByEpisodeID(a.AniDB_EpisodeID)?.EpisodeID ?? 0)
                                .Select(b => RepoFactory.VideoLocal.GetByHash(b.Hash))
                                .Count(b => b != null) > 0)
                .Select(a => a.EpisodeTypeEnum)
                .Distinct()
                .Count();
        }

        public MovieDB_Movie GetMovieDB()
        {
            var movieDBXRef = RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(AniDB_ID, CrossRefType.MovieDB);
            if (movieDBXRef?.CrossRefID == null || !int.TryParse(movieDBXRef.CrossRefID, out int movieID)) return null;
            var movieDB = RepoFactory.MovieDb_Movie.GetByOnlineID(movieID);
            return movieDB;
        }


#region TvDB

        public List<CrossRef_AniDB_TvDB> GetCrossRefTvDB()
        {
            return RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(AniDB_ID);
        }

        public List<TvDB_Series> GetTvDBSeries()
        {
            List<TvDB_Series> sers = new List<TvDB_Series>();

            List<CrossRef_AniDB_TvDB> xrefs = GetCrossRefTvDB();
            if (xrefs == null || xrefs.Count == 0) return sers;

            foreach (CrossRef_AniDB_TvDB xref in xrefs)
            {
                var series = xref.GetTvDBSeries();
                if (series != null)
                    sers.Add(series);
            }

            return sers;
        }

        #endregion

        #region Trakt

        public List<CrossRef_AniDB_TraktV2> GetCrossRefTraktV2()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetCrossRefTraktV2(session);
            }
        }

        public List<CrossRef_AniDB_TraktV2> GetCrossRefTraktV2(ISession session)
        {
            return RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(session, AniDB_ID);
        }

        public List<Trakt_Show> GetTraktShow()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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

        public CrossRef_AniDB_Other CrossRefMovieDB => RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(AniDB_ID, CrossRefType.MovieDB);

        public List<CrossRef_AniDB_MAL> CrossRefMAL => RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID(AniDB_ID);

        public CL_AnimeSeries_User GetUserContract(int userid, HashSet<GroupFilterConditionType> types = null)
        {
            try
            {
                CL_AnimeSeries_User contract = (CL_AnimeSeries_User) Contract?.Clone();
                if (contract == null)
                {
                    logger.Trace($"Series with ID [{AniDB_ID}] has a null contract on get. Updating");
                    RepoFactory.AnimeSeries.Save(this, false, false, true);
                    contract = (CL_AnimeSeries_User) _contract?.Clone();
                }

                if (contract == null)
                {
                    logger.Warn($"Series with ID [{AniDB_ID}] has a null contract even after updating");
                    return null;
                }

                SVR_AnimeSeries_User rr = GetUserRecord(userid);
                if (rr != null)
                {
                    contract.UnwatchedEpisodeCount = rr.UnwatchedEpisodeCount;
                    contract.WatchedEpisodeCount = rr.WatchedEpisodeCount;
                    contract.WatchedDate = rr.WatchedDate;
                    contract.PlayedCount = rr.PlayedCount;
                    contract.WatchedCount = rr.WatchedCount;
                    contract.StoppedCount = rr.StoppedCount;
                    contract.AniDBAnime.AniDBAnime.FormattedTitle = GetSeriesName();
                    return contract;
                }

                if (types != null)
                {
                    if (!types.Contains(GroupFilterConditionType.HasUnwatchedEpisodes))
                        types.Add(GroupFilterConditionType.HasUnwatchedEpisodes);
                    if (!types.Contains(GroupFilterConditionType.EpisodeWatchedDate))
                        types.Add(GroupFilterConditionType.EpisodeWatchedDate);
                    if (!types.Contains(GroupFilterConditionType.HasWatchedEpisodes))
                        types.Add(GroupFilterConditionType.HasWatchedEpisodes);
                }

                if (contract.AniDBAnime?.AniDBAnime != null)
                    contract.AniDBAnime.AniDBAnime.FormattedTitle = GetSeriesName();

                return contract;
            }
            catch
            {
                return null;
            }
        }

        public Video GetPlexContract(int userid)
        {
            CL_AnimeSeries_User ser = GetUserContract(userid);
            Video v = GetOrCreateUserRecord(userid).PlexContract;
            v.Title = ser.AniDBAnime.AniDBAnime.FormattedTitle;
            return v;
        }

        private SVR_AnimeSeries_User GetOrCreateUserRecord(int userid)
        {
            SVR_AnimeSeries_User rr = GetUserRecord(userid);
            if (rr != null)
                return rr;
            rr = new SVR_AnimeSeries_User(userid, AnimeSeriesID)
            {
                WatchedCount = 0,
                UnwatchedEpisodeCount = 0,
                PlayedCount = 0,
                StoppedCount = 0,
                WatchedEpisodeCount = 0,
                WatchedDate = null
            };
            RepoFactory.AnimeSeries_User.Save(rr);
            return rr;
        }

        public SVR_AnimeEpisode GetLastEpisodeWatched(int userID)
        {
            SVR_AnimeEpisode watchedep = null;
            SVR_AnimeEpisode_User userRecordWatched = null;

            foreach (SVR_AnimeEpisode ep in GetAnimeEpisodes())
            {
                SVR_AnimeEpisode_User userRecord = ep.GetUserRecord(userID);
                if (userRecord != null && ep.AniDB_Episode != null && ep.EpisodeTypeEnum == EpisodeType.Episode)
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

        public SVR_AnimeSeries_User GetUserRecord(int userID)
        {
            return RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, AnimeSeriesID);
        }


        public SVR_AniDB_Anime GetAnime()
        {
            return RepoFactory.AniDB_Anime.GetByAnimeID(AniDB_ID);
        }

        public DateTime AirDate
        {
            get
            {
                var anime = GetAnime();
                if (anime?.AirDate != null)
                    return anime.AirDate.Value;
                // This will be slower, but hopefully more accurate
                DateTime? ep = GetAnimeEpisodes()
                    .Select(a => a.AniDB_Episode.GetAirDateAsDate()).Where(a => a != null).OrderBy(a => a)
                    .FirstOrDefault();
                if (ep != null)
                    return ep.Value;
                return DateTime.MinValue;
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

        public void Populate(SVR_AniDB_Anime anime)
        {
            AniDB_ID = anime.AnimeID;
            LatestLocalEpisodeNumber = 0;
            DateTimeUpdated = DateTime.Now;
            DateTimeCreated = DateTime.Now;
            UpdatedAt = DateTime.Now;
            SeriesNameOverride = string.Empty;
        }

        public void CreateAnimeEpisodes(SVR_AniDB_Anime anime = null)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                CreateAnimeEpisodes(session, anime);
            }
        }

        public void CreateAnimeEpisodes(ISession session, SVR_AniDB_Anime anime = null)
        {
            anime = anime ?? GetAnime();
            if (anime == null) return;

            var eps = anime.GetAniDBEpisodes();
            int one_forth = (int) Math.Round(eps.Count / 4D, 0, MidpointRounding.AwayFromZero);
            int one_half = (int) Math.Round(eps.Count / 2D, 0, MidpointRounding.AwayFromZero);
            int three_forths = (int) Math.Round(eps.Count * 3 / 4D, 0, MidpointRounding.AwayFromZero);

            logger.Trace($"Generating {eps.Count} episodes for {anime.MainTitle}");
            for (int i = 0; i < eps.Count; i++)
            {
                if (i == one_forth) logger.Trace($"Generating episodes for {anime.MainTitle}: 25%");
                if (i == one_half) logger.Trace($"Generating episodes for {anime.MainTitle}: 50%");
                if (i == three_forths) logger.Trace($"Generating episodes for {anime.MainTitle}: 75%");
                if (i == eps.Count - 1) logger.Trace($"Generating episodes for {anime.MainTitle}: 100%");
                var ep = eps[i];
                ep.CreateAnimeEpisode(session, AnimeSeriesID);
            }
        }

        public bool NeedsEpisodeUpdate()
        {
            SVR_AniDB_Anime anime = GetAnime();
            if (anime == null) return false;

            return anime.GetAniDBEpisodes()
                .Select(episode => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(episode.EpisodeID))
                .Any(ep => ep == null) || GetAnimeEpisodes()
                .Select(episode => RepoFactory.AniDB_Episode.GetByEpisodeID(episode.AniDB_EpisodeID))
                .Any(ep => ep == null);
        }

        /// <summary>
        /// Gets the direct parent AnimeGroup this series belongs to
        /// </summary>
        public SVR_AnimeGroup AnimeGroup => RepoFactory.AnimeGroup.GetByID(AnimeGroupID);

        /// <summary>
        /// Gets the very top level AnimeGroup which this series belongs to
        /// </summary>
        public SVR_AnimeGroup TopLevelAnimeGroup
        {
            get
            {
                SVR_AnimeGroup parentGroup = RepoFactory.AnimeGroup.GetByID(AnimeGroupID);

                while (parentGroup?.AnimeGroupParentID != null)
                    parentGroup = RepoFactory.AnimeGroup.GetByID(parentGroup.AnimeGroupParentID.Value);
                return parentGroup;
            }
        }

        public List<SVR_AnimeGroup> AllGroupsAbove
        {
            get
            {
                List<SVR_AnimeGroup> grps = new List<SVR_AnimeGroup>();
                try
                {
                    int? groupID = AnimeGroupID;
                    while (groupID.HasValue)
                    {
                        SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(groupID.Value);
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
                    logger.Error(ex, ex.ToString());
                }
                return grps;
            }
        }

        public void UpdateGroupFilters(HashSet<GroupFilterConditionType> types, SVR_JMMUser user = null)
        {
            IReadOnlyList<SVR_JMMUser> users = new List<SVR_JMMUser> {user};
            if (user == null)
                users = RepoFactory.JMMUser.GetAll();
            List<SVR_GroupFilter> tosave = new List<SVR_GroupFilter>();

            HashSet<GroupFilterConditionType> n = new HashSet<GroupFilterConditionType>(types);
            var gfs = RepoFactory.GroupFilter.GetWithConditionTypesAndAll(n);
            logger.Trace($"Updating {gfs.Count} Group Filters from Series {GetAnime().MainTitle}");
            foreach (SVR_GroupFilter gf in gfs)
            {
                if (gf.UpdateGroupFilterFromSeries(Contract, null))
                    if (!tosave.Contains(gf))
                        tosave.Add(gf);
                foreach (SVR_JMMUser u in users)
                {
                    CL_AnimeSeries_User cgrp = GetUserContract(u.JMMUserID, n);

                    if (gf.UpdateGroupFilterFromSeries(cgrp, u))
                        if (!tosave.Contains(gf))
                            tosave.Add(gf);
                }
            }
            RepoFactory.GroupFilter.Save(tosave);
        }

        public void DeleteFromFilters()
        {
            foreach (SVR_GroupFilter gf in RepoFactory.GroupFilter.GetAll())
            {
                bool change = false;
                foreach (int k in gf.SeriesIds.Keys)
                    if (gf.SeriesIds[k].Contains(AnimeSeriesID))
                    {
                        gf.SeriesIds[k].Remove(AnimeSeriesID);
                        change = true;
                    }
                if (change)
                    RepoFactory.GroupFilter.Save(gf);
            }
        }

        public static Dictionary<int, HashSet<GroupFilterConditionType>> BatchUpdateContracts(ISessionWrapper session,
            IReadOnlyCollection<SVR_AnimeSeries> seriesBatch, bool onlyStats = false)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (seriesBatch == null)
                throw new ArgumentNullException(nameof(seriesBatch));

            var grpFilterCondTypesPerSeries = new Dictionary<int, HashSet<GroupFilterConditionType>>();

            if (seriesBatch.Count == 0)
                return grpFilterCondTypesPerSeries;

            var animeIds = new Lazy<int[]>(() => seriesBatch.Select(s => s.AniDB_ID).ToArray(), false);
            var tvDbByAnime = new Lazy<ILookup<int, Tuple<CrossRef_AniDB_TvDB, TvDB_Series>>>(
                () => RepoFactory.TvDB_Series.GetByAnimeIDs(session, animeIds.Value), false);
            var movieByAnime = new Lazy<Dictionary<int, Tuple<CrossRef_AniDB_Other, MovieDB_Movie>>>(
                () => RepoFactory.MovieDb_Movie.GetByAnimeIDs(session, animeIds.Value), false);
            var malXrefByAnime = new Lazy<ILookup<int, CrossRef_AniDB_MAL>>(
                () => RepoFactory.CrossRef_AniDB_MAL.GetByAnimeIDs(session, animeIds.Value), false);
            var defImagesByAnime = new Lazy<Dictionary<int, DefaultAnimeImages>>(
                () => RepoFactory.AniDB_Anime.GetDefaultImagesByAnime(session, animeIds.Value), false);

            foreach (SVR_AnimeSeries series in seriesBatch)
            {
                CL_AnimeSeries_User contract = (CL_AnimeSeries_User) series.Contract?.Clone();
                bool seriesOnlyStats = onlyStats;

                if (contract == null)
                {
                    contract = new CL_AnimeSeries_User();
                    seriesOnlyStats = false;
                }

                contract.AniDB_ID = series.AniDB_ID;
                contract.AnimeGroupID = series.AnimeGroupID;
                contract.AnimeSeriesID = series.AnimeSeriesID;
                contract.DateTimeUpdated = series.DateTimeUpdated;
                contract.DateTimeCreated = series.DateTimeCreated;
                contract.DefaultAudioLanguage = series.DefaultAudioLanguage;
                contract.DefaultSubtitleLanguage = series.DefaultSubtitleLanguage;
                contract.LatestLocalEpisodeNumber = series.LatestLocalEpisodeNumber;
                contract.LatestEpisodeAirDate = series.LatestEpisodeAirDate;
                contract.AirsOn = series.AirsOn;
                contract.EpisodeAddedDate = series.EpisodeAddedDate;
                contract.MissingEpisodeCount = series.MissingEpisodeCount;
                contract.MissingEpisodeCountGroups = series.MissingEpisodeCountGroups;
                contract.SeriesNameOverride = series.SeriesNameOverride;
                contract.DefaultFolder = series.DefaultFolder;
                contract.PlayedCount = 0;
                contract.StoppedCount = 0;
                contract.UnwatchedEpisodeCount = 0;
                contract.WatchedCount = 0;
                contract.WatchedDate = null;
                contract.WatchedEpisodeCount = 0;

                if (!seriesOnlyStats)
                {
                    // AniDB contract
                    SVR_AniDB_Anime animeRec = series.GetAnime();

                    if (animeRec != null)
                    {
                        contract.AniDBAnime = (CL_AniDB_AnimeDetailed) animeRec.Contract.Clone();

                        CL_AniDB_Anime aniDbAnime = contract.AniDBAnime.AniDBAnime;

                        if (!defImagesByAnime.Value.TryGetValue(animeRec.AnimeID, out DefaultAnimeImages defImages))
                            defImages = new DefaultAnimeImages { AnimeID = animeRec.AnimeID };

                        aniDbAnime.DefaultImagePoster = defImages.GetPosterContractNoBlanks();
                        aniDbAnime.DefaultImageFanart = defImages.GetFanartContractNoBlanks(aniDbAnime);
                        aniDbAnime.DefaultImageWideBanner = defImages.WideBanner?.ToContract();
                    }

                    // TvDB contracts
                    var tvDbCrossRefs = tvDbByAnime.Value[series.AniDB_ID].ToList();

                    foreach (var missingTvDbSeries in tvDbCrossRefs.Where(cr => cr.Item2 == null)
                        .Select(cr => cr.Item1))
                        logger.Warn("You are missing database information for TvDB series: {0} - {1}",
                            missingTvDbSeries.TvDBID, missingTvDbSeries.GetTvDBSeries()?.SeriesName ?? "Series Not Found");

                    contract.CrossRefAniDBTvDBV2 = RepoFactory.CrossRef_AniDB_TvDB.GetV2LinksFromAnime(series.AniDB_ID);
                    contract.TvDB_Series = tvDbCrossRefs
                        .Select(s => s.Item2)
                        .ToList();

                    // MovieDB contracts

                    if (movieByAnime.Value.TryGetValue(series.AniDB_ID, out Tuple<CrossRef_AniDB_Other, MovieDB_Movie> movieDbInfo))
                    {
                        contract.CrossRefAniDBMovieDB = movieDbInfo.Item1;
                        contract.MovieDB_Movie = movieDbInfo.Item2;
                    }
                    else
                    {
                        contract.CrossRefAniDBMovieDB = null;
                        contract.MovieDB_Movie = null;
                    }

                    // MAL contracts
                    contract.CrossRefAniDBMAL = malXrefByAnime.Value[series.AniDB_ID]
                        .ToList();
                }

                HashSet<GroupFilterConditionType> typesChanged = GetConditionTypesChanged(series.Contract, contract);

                series.Contract = contract;
                grpFilterCondTypesPerSeries.Add(series.AnimeSeriesID, typesChanged);
            }

            return grpFilterCondTypesPerSeries;
        }

        public HashSet<GroupFilterConditionType> UpdateContract(bool onlystats = false)
        {
            DateTime start = DateTime.Now;
            TimeSpan ts;
            CL_AnimeSeries_User contract = (CL_AnimeSeries_User) Contract?.Clone();
            ts = DateTime.Now - start;
                logger.Trace($"While Updating SERIES {GetAnime()?.MainTitle ?? AniDB_ID.ToString()}, Cloned Series Contract in {ts.TotalMilliseconds}ms");
            if (contract == null)
            {
                contract = new CL_AnimeSeries_User();
                onlystats = false;
            }

            contract.AniDB_ID = AniDB_ID;
            contract.AnimeGroupID = AnimeGroupID;
            contract.AnimeSeriesID = AnimeSeriesID;
            contract.DateTimeUpdated = DateTimeUpdated;
            contract.DateTimeCreated = DateTimeCreated;
            contract.DefaultAudioLanguage = DefaultAudioLanguage;
            contract.DefaultSubtitleLanguage = DefaultSubtitleLanguage;
            contract.LatestLocalEpisodeNumber = LatestLocalEpisodeNumber;
            contract.LatestEpisodeAirDate = LatestEpisodeAirDate;
            contract.AirsOn = AirsOn;
            contract.EpisodeAddedDate = EpisodeAddedDate;
            contract.MissingEpisodeCount = MissingEpisodeCount;
            contract.MissingEpisodeCountGroups = MissingEpisodeCountGroups;
            contract.SeriesNameOverride = SeriesNameOverride;
            contract.DefaultFolder = DefaultFolder;
            contract.PlayedCount = 0;
            contract.StoppedCount = 0;
            contract.UnwatchedEpisodeCount = 0;
            contract.WatchedCount = 0;
            contract.WatchedDate = null;
            contract.WatchedEpisodeCount = 0;
            if (onlystats)
            {
                start = DateTime.Now;
                HashSet<GroupFilterConditionType> types2 = GetConditionTypesChanged(Contract, contract);
                Contract = contract;
                ts = DateTime.Now - start;
                    logger.Trace($"While Updating SERIES {GetAnime()?.MainTitle ?? AniDB_ID.ToString()}, Got GroupFilterConditionTypesChanged in {ts.TotalMilliseconds}ms");
                return types2;
            }
            SVR_AniDB_Anime animeRec = GetAnime();
            List<CrossRef_AniDB_TvDB> tvDBCrossRefs = GetCrossRefTvDB();
            CrossRef_AniDB_Other movieDBCrossRef = CrossRefMovieDB;
            MovieDB_Movie movie = null;
            if (movieDBCrossRef != null)
                movie = movieDBCrossRef.GetMovieDB_Movie();
            List<TvDB_Series> sers = new List<TvDB_Series>();
            foreach (CrossRef_AniDB_TvDB xref in tvDBCrossRefs)
            {
                TvDB_Series tvser = xref.GetTvDBSeries();
                if (tvser != null)
                    sers.Add(tvser);
                else
                    logger.Warn("You are missing database information for TvDB series: {0}", xref.TvDBID);
            }
            // get AniDB data
            if (animeRec != null)
            {
                start = DateTime.Now;
                if (animeRec.Contract == null)
                    RepoFactory.AniDB_Anime.Save(animeRec);
                contract.AniDBAnime = (CL_AniDB_AnimeDetailed) animeRec.Contract.Clone();
                ts = DateTime.Now - start;
                    logger.Trace($"While Updating SERIES {GetAnime()?.MainTitle ?? AniDB_ID.ToString()}, Got and Cloned AniDB_Anime Contract in {ts.TotalMilliseconds}ms");
                contract.AniDBAnime.AniDBAnime.DefaultImagePoster = animeRec.GetDefaultPoster()?.ToClient();
                if (contract.AniDBAnime.AniDBAnime.DefaultImagePoster == null)
                {
                    ImageDetails im = animeRec.GetDefaultPosterDetailsNoBlanks();
                    if (im != null)
                        contract.AniDBAnime.AniDBAnime.DefaultImagePoster = new CL_AniDB_Anime_DefaultImage
                        {
                            AnimeID = im.ImageID,
                            ImageType = (int)im.ImageType
                        };
                }
                contract.AniDBAnime.AniDBAnime.DefaultImageFanart = animeRec.GetDefaultFanart()?.ToClient();
                if (contract.AniDBAnime.AniDBAnime.DefaultImageFanart == null)
                {
                    ImageDetails im = animeRec.GetDefaultFanartDetailsNoBlanks();
                    if (im != null)
                        contract.AniDBAnime.AniDBAnime.DefaultImageFanart = new CL_AniDB_Anime_DefaultImage
                        {
                            AnimeID = im.ImageID,
                            ImageType = (int)im.ImageType
                        };
                }
                contract.AniDBAnime.AniDBAnime.DefaultImageWideBanner = animeRec.GetDefaultWideBanner()?.ToClient();
            }

            contract.CrossRefAniDBTvDBV2 = RepoFactory.CrossRef_AniDB_TvDB.GetV2LinksFromAnime(AniDB_ID);


            contract.TvDB_Series = sers;
            contract.CrossRefAniDBMovieDB = null;
            if (movieDBCrossRef != null)
            {
                contract.CrossRefAniDBMovieDB = movieDBCrossRef;
                contract.MovieDB_Movie = movie;
            }
            contract.CrossRefAniDBMAL = CrossRefMAL?.ToList() ?? new List<CrossRef_AniDB_MAL>();
            start = DateTime.Now;
            HashSet<GroupFilterConditionType> types = GetConditionTypesChanged(Contract, contract);
            ts = DateTime.Now - start;
                logger.Trace($"While Updating SERIES {GetAnime()?.MainTitle ?? AniDB_ID.ToString()}, Got GroupFilterConditionTypesChanged in {ts.TotalMilliseconds}ms");
            Contract = contract;
            return types;
        }


        public static HashSet<GroupFilterConditionType> GetConditionTypesChanged(CL_AnimeSeries_User oldcontract,
            CL_AnimeSeries_User newcontract)
        {
            HashSet<GroupFilterConditionType> h = new HashSet<GroupFilterConditionType>();

            if (oldcontract == null ||
                (oldcontract.AniDBAnime.AniDBAnime.EndDate.HasValue &&
                 oldcontract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now &&
                 !(oldcontract.MissingEpisodeCount > 0 ||
                   oldcontract.MissingEpisodeCountGroups > 0)) !=
                (newcontract.AniDBAnime.AniDBAnime.EndDate.HasValue &&
                 newcontract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now &&
                 !(newcontract.MissingEpisodeCount > 0 || newcontract.MissingEpisodeCountGroups > 0)))
                h.Add(GroupFilterConditionType.CompletedSeries);
            if (oldcontract == null ||
                (oldcontract.MissingEpisodeCount > 0 || oldcontract.MissingEpisodeCountGroups > 0) !=
                (newcontract.MissingEpisodeCount > 0 || newcontract.MissingEpisodeCountGroups > 0))
                h.Add(GroupFilterConditionType.MissingEpisodes);
            if (oldcontract == null ||
                !oldcontract.AniDBAnime.AniDBAnime.GetAllTags()
                    .SetEquals(newcontract.AniDBAnime.AniDBAnime.GetAllTags()))
                h.Add(GroupFilterConditionType.Tag);
            if (oldcontract == null ||
                oldcontract.AniDBAnime.AniDBAnime.AirDate != newcontract.AniDBAnime.AniDBAnime.AirDate)
                h.Add(GroupFilterConditionType.AirDate);
            if (oldcontract == null ||
                ((oldcontract.CrossRefAniDBTvDBV2 == null || oldcontract.CrossRefAniDBTvDBV2.Count == 0) !=
                 (newcontract.CrossRefAniDBTvDBV2 == null || newcontract.CrossRefAniDBTvDBV2.Count == 0)))
                h.Add(GroupFilterConditionType.AssignedTvDBInfo);
            if (oldcontract == null ||
                ((oldcontract.CrossRefAniDBMAL == null || oldcontract.CrossRefAniDBMAL.Count == 0) !=
                 (newcontract.CrossRefAniDBMAL == null || newcontract.CrossRefAniDBMAL.Count == 0)))
                h.Add(GroupFilterConditionType.AssignedMALInfo);
            if (oldcontract == null ||
                (oldcontract.CrossRefAniDBMovieDB == null != (newcontract.CrossRefAniDBMovieDB == null)))
                h.Add(GroupFilterConditionType.AssignedMovieDBInfo);
            if (oldcontract == null ||
                ((oldcontract.CrossRefAniDBMovieDB == null) &&
                 (oldcontract.CrossRefAniDBTvDBV2 == null || oldcontract.CrossRefAniDBTvDBV2.Count == 0) !=
                 ((newcontract.CrossRefAniDBMovieDB == null) &&
                  (newcontract.CrossRefAniDBTvDBV2 == null || newcontract.CrossRefAniDBTvDBV2.Count == 0))))
                h.Add(GroupFilterConditionType.AssignedTvDBOrMovieDBInfo);
            if (oldcontract == null ||
                oldcontract.AniDBAnime.AniDBAnime.AnimeType != newcontract.AniDBAnime.AniDBAnime.AnimeType)
                h.Add(GroupFilterConditionType.AnimeType);
            if (oldcontract == null ||
                !oldcontract.AniDBAnime.Stat_AllVideoQuality.SetEquals(newcontract.AniDBAnime.Stat_AllVideoQuality) ||
                !oldcontract.AniDBAnime.Stat_AllVideoQuality_Episodes.SetEquals(
                    newcontract.AniDBAnime.Stat_AllVideoQuality_Episodes))
                h.Add(GroupFilterConditionType.VideoQuality);
            if (oldcontract == null ||
                oldcontract.AniDBAnime.AniDBAnime.VoteCount != newcontract.AniDBAnime.AniDBAnime.VoteCount ||
                oldcontract.AniDBAnime.AniDBAnime.TempVoteCount != newcontract.AniDBAnime.AniDBAnime.TempVoteCount ||
                oldcontract.AniDBAnime.AniDBAnime.Rating != newcontract.AniDBAnime.AniDBAnime.Rating ||
                oldcontract.AniDBAnime.AniDBAnime.TempRating != newcontract.AniDBAnime.AniDBAnime.TempRating)
                h.Add(GroupFilterConditionType.AniDBRating);
            if (oldcontract == null || oldcontract.DateTimeCreated != newcontract.DateTimeCreated)
                h.Add(GroupFilterConditionType.SeriesCreatedDate);
            if (oldcontract == null || oldcontract.EpisodeAddedDate != newcontract.EpisodeAddedDate)
                h.Add(GroupFilterConditionType.EpisodeAddedDate);
            if (oldcontract == null ||
                (oldcontract.AniDBAnime.AniDBAnime.EndDate.HasValue &&
                 oldcontract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now) !=
                (newcontract.AniDBAnime.AniDBAnime.EndDate.HasValue &&
                 newcontract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now))
                h.Add(GroupFilterConditionType.FinishedAiring);
            if (oldcontract == null ||
                oldcontract.MissingEpisodeCountGroups > 0 != newcontract.MissingEpisodeCountGroups > 0)
                h.Add(GroupFilterConditionType.MissingEpisodesCollecting);
            if (oldcontract == null ||
                !oldcontract.AniDBAnime.Stat_AudioLanguages.SetEquals(newcontract.AniDBAnime.Stat_AudioLanguages))
                h.Add(GroupFilterConditionType.AudioLanguage);
            if (oldcontract == null ||
                !oldcontract.AniDBAnime.Stat_SubtitleLanguages.SetEquals(newcontract.AniDBAnime.Stat_SubtitleLanguages))
                h.Add(GroupFilterConditionType.SubtitleLanguage);
            if (oldcontract == null ||
                oldcontract.AniDBAnime.AniDBAnime.EpisodeCount != newcontract.AniDBAnime.AniDBAnime.EpisodeCount)
                h.Add(GroupFilterConditionType.EpisodeCount);
            if (oldcontract == null ||
                !oldcontract.AniDBAnime.CustomTags.Select(a => a.TagName)
                    .ToHashSet()
                    .SetEquals(newcontract.AniDBAnime.CustomTags.Select(a => a.TagName).ToHashSet()))
                h.Add(GroupFilterConditionType.CustomTags);
            if (oldcontract == null || oldcontract.LatestEpisodeAirDate != newcontract.LatestEpisodeAirDate)
                h.Add(GroupFilterConditionType.LatestEpisodeAirDate);
            int oldyear = -1;
            int newyear = -1;
            if (oldcontract?.AniDBAnime?.AniDBAnime?.AirDate != null)
                oldyear = oldcontract.AniDBAnime.AniDBAnime.AirDate.Value.Year;
            if (newcontract?.AniDBAnime?.AniDBAnime?.AirDate != null)
                newyear = newcontract.AniDBAnime.AniDBAnime.AirDate.Value.Year;
            if (oldyear != newyear)
                h.Add(GroupFilterConditionType.Year);

            if (oldcontract?.AniDBAnime?.Stat_AllSeasons == null || !oldcontract.AniDBAnime.Stat_AllSeasons.SetEquals(newcontract.AniDBAnime.Stat_AllSeasons))
                h.Add(GroupFilterConditionType.Season);

            //TODO This three should be moved to AnimeSeries_User in the future...
            if (oldcontract == null ||
                ((oldcontract.AniDBAnime.UserVote != null) &&
                 (oldcontract.AniDBAnime.UserVote.VoteType == (int) AniDBVoteType.Anime)) !=
                ((newcontract.AniDBAnime.UserVote != null) &&
                 (newcontract.AniDBAnime.UserVote.VoteType == (int) AniDBVoteType.Anime)))
                h.Add(GroupFilterConditionType.UserVoted);
            if (oldcontract == null ||
                oldcontract.AniDBAnime.UserVote != null != (newcontract.AniDBAnime.UserVote != null))
                h.Add(GroupFilterConditionType.UserVotedAny);
            if (oldcontract == null ||
                ((oldcontract.AniDBAnime.UserVote?.VoteValue ?? 0) !=
                 (newcontract.AniDBAnime.UserVote?.VoteValue ?? 0)))
                h.Add(GroupFilterConditionType.UserRating);

            return h;
        }

        public override string ToString()
        {
            return $"Series: {GetAnime().MainTitle} ({AnimeSeriesID})";
            //return string.Empty;
        }

        internal class EpisodeList : List<EpisodeList.StatEpisodes>
        {
            public EpisodeList(AnimeType ept)
            {
                AnimeType = ept;
            }

            private AnimeType AnimeType { get; set; }

            readonly Regex partmatch =
                new Regex("part (\\d.*?) of (\\d.*)");

            readonly Regex remsymbols = new Regex("[^A-Za-z0-9 ]");

            private readonly Regex remmultispace =
                new Regex("\\s+");

            public void Add(SVR_AnimeEpisode ep, bool available)
            {
                if (AnimeType == AnimeType.OVA || AnimeType == AnimeType.Movie)
                {
                    string ename = ep.Title;
                    bool empty = string.IsNullOrEmpty(ename);
                    Match m = null;
                    if (!empty) m = partmatch.Match(ename);
                    StatEpisodes.StatEpisode s = new StatEpisodes.StatEpisode
                    {
                        Available = available
                    };
                    if (m?.Success ?? false)
                    {
                        int.TryParse(m.Groups[1].Value, out int part_number);
                        int.TryParse(m.Groups[2].Value, out int part_count);
                        string rname = partmatch.Replace(ename, string.Empty);
                        rname = remsymbols.Replace(rname, string.Empty);
                        rname = remmultispace.Replace(rname, " ");


                        s.EpisodeType = StatEpisodes.StatEpisode.EpType.Part;
                        s.PartCount = part_count;
                        s.Match = rname.Trim();
                        if (s.Match == "complete movie" || s.Match == "movie" || s.Match == "ova")
                            s.Match = string.Empty;
                    }
                    else
                    {
                        if (empty || ename == "complete movie" || ename == "movie" || ename == "ova")
                        {
                            s.Match = string.Empty;
                        }
                        else
                        {
                            string rname = partmatch.Replace(ep.Title, string.Empty);
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
                            if (ss.Match == s.Match)
                            {
                                fnd = k;
                                break;
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
                    StatEpisodes.StatEpisode es = new StatEpisodes.StatEpisode
                    {
                        Match = string.Empty,
                        EpisodeType = StatEpisodes.StatEpisode.EpType.Complete,
                        PartCount = 0,
                        Available = available
                    };
                    eps.Add(es);
                    Add(eps);
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
                        int maxcnt = this.Select(k => k.PartCount).Concat(new[] {0}).Max();
                        int[] parts = new int[maxcnt + 1];
                        foreach (StatEpisode k in this)
                        {
                            switch (k.EpisodeType)
                            {
                                case StatEpisode.EpType.Complete when k.Available:
                                    return true;
                                case StatEpisode.EpType.Part when k.Available:
                                    parts[k.PartCount]++;
                                    if (parts[k.PartCount] == k.PartCount)
                                        return true;
                                    break;
                            }
                        }
                        return false;
                    }
                }
            }
        }

        public void MoveSeries(SVR_AnimeGroup newGroup)
        {
            // Update the stats for the series and group.
            AnimeGroupID = newGroup.AnimeGroupID;
            DateTimeUpdated = DateTime.Now;
            UpdateStats(true, true, true);

            var oldGroup = RepoFactory.AnimeGroup.GetByID(AnimeGroupID);
            if (oldGroup != null)
            {
                // This was the only one series in the group so delete the now orphan group.
                if (oldGroup.GetAllSeries().Count == 0)
                    oldGroup.DeleteGroup(false);
                // Update the top group 
                var topGroup = oldGroup.TopLevelAnimeGroup;
                if (topGroup.AnimeGroupID != oldGroup.AnimeGroupID)
                    topGroup.UpdateStatsFromTopLevel(true, true, true);
            }
        }

        public void QueueUpdateStats()
        {
            CommandRequest_RefreshAnime cmdRefreshAnime = new CommandRequest_RefreshAnime(AniDB_ID);
            cmdRefreshAnime.Save();
        }

        public void UpdateStats(bool watchedStats, bool missingEpsStats, bool updateAllGroupsAbove)
        {
            DateTime start = DateTime.Now;
            DateTime initialStart = DateTime.Now;
            string name = GetAnime()?.MainTitle ?? AniDB_ID.ToString();
            logger.Info(
                $"Starting Updating STATS for SERIES {name} - Watched Stats: {watchedStats}, Missing Episodes: {missingEpsStats}, Update Group Stats: {updateAllGroupsAbove}");

            DateTime startEps = DateTime.Now;
            List<SVR_AnimeEpisode> eps = GetAnimeEpisodes().Where(a => a.AniDB_Episode != null).ToList();
            TimeSpan tsEps = DateTime.Now - startEps;
            logger.Trace($"Got episodes for SERIES {name} in {tsEps.TotalMilliseconds}ms");

            if (watchedStats)
            {
                foreach (SVR_JMMUser juser in RepoFactory.JMMUser.GetAll())
                {
                    SVR_AnimeSeries_User userRecord = GetUserRecord(juser.JMMUserID) ??
                                                      new SVR_AnimeSeries_User(juser.JMMUserID, AnimeSeriesID);

                    int unwatchedCount = 0;
                    int watchedCount = 0;
                    int watchedEpisodeCount = 0;
                    DateTime? watchedDate = null;

                    object lck = new object();
                    eps.AsParallel().Where(ep =>
                        ep.GetVideoLocals().Any() && (ep.EpisodeTypeEnum == EpisodeType.Episode ||
                                                      ep.EpisodeTypeEnum == EpisodeType.Special)).ForAll(
                        ep =>
                        {
                            SVR_AnimeEpisode_User epUserRecord = ep.GetUserRecord(juser.JMMUserID);

                            if (epUserRecord?.WatchedDate == null)
                            {
                                Interlocked.Increment(ref unwatchedCount);
                                return;
                            }

                            lock (lck)
                            {
                                if (watchedDate == null)
                                    watchedDate = epUserRecord.WatchedDate;
                                else if (epUserRecord.WatchedDate > watchedDate)
                                    watchedDate = epUserRecord.WatchedDate;
                            }

                            Interlocked.Increment(ref watchedEpisodeCount);
                            Interlocked.Add(ref watchedCount, epUserRecord.WatchedCount);
                        });
                    userRecord.UnwatchedEpisodeCount = unwatchedCount;
                    userRecord.WatchedEpisodeCount = watchedEpisodeCount;
                    userRecord.WatchedCount = watchedCount;
                    userRecord.WatchedDate = watchedDate;
                    RepoFactory.AnimeSeries_User.Save(userRecord);
                }
            }

            TimeSpan ts = DateTime.Now - start;
            logger.Trace($"Updated WATCHED stats for SERIES {name} in {ts.TotalMilliseconds}ms");
            start = DateTime.Now;

            if (missingEpsStats)
            {
                AnimeType animeType = GetAnime()?.GetAnimeTypeEnum() ?? AnimeType.TVSeries;

                MissingEpisodeCount = 0;
                MissingEpisodeCountGroups = 0;

                // get all the group status records
                List<AniDB_GroupStatus> grpStatuses = RepoFactory.AniDB_GroupStatus.GetByAnimeID(AniDB_ID);

                // find all the episodes for which the user has a file
                // from this we can determine what their latest episode number is
                // find out which groups the user is collecting

                int latestLocalEpNumber = 0;
                DateTime? lastEpAirDate = null;
                EpisodeList epReleasedList = new EpisodeList(animeType);
                EpisodeList epGroupReleasedList = new EpisodeList(animeType);
                Dictionary<DayOfWeek, int> daysofweekcounter = new Dictionary<DayOfWeek, int>();

                List<int> userReleaseGroups = new List<int>();
                // This was always Episodes only. Maybe in the future, we'll have a reliable way to check specials.
                eps.AsParallel().Where(a => a.EpisodeTypeEnum == EpisodeType.Episode).ForAll(ep =>
                {
                    var vids = ep.GetVideoLocals();

                    lock (userReleaseGroups)
                    {
                        userReleaseGroups.AddRange(vids.Select(a => a.GetAniDBFile())
                            .Where(a => a != null && !userReleaseGroups.Contains(a.GroupID)).Select(a => a.GroupID));
                    }

                    AniDB_Episode aniEp = ep.AniDB_Episode;
                    int thisEpNum = aniEp.EpisodeNumber;

                    if (thisEpNum > latestLocalEpNumber && vids.Any())
                        latestLocalEpNumber = thisEpNum;

                    DateTime? airdate = ep.AniDB_Episode.GetAirDateAsDate();

                    // Only count episodes that have already aired
                    if (!aniEp.GetFutureDated())
                    {
                        // Only convert if we have time info
                        DateTime airdateLocal;
                        // ignore the possible null on airdate, it's checked in GetFutureDated
                        if (airdate.Value.Hour == 0 && airdate.Value.Minute == 0 && airdate.Value.Second == 0)
                            airdateLocal = airdate.Value;
                        else
                        {
                            airdateLocal = DateTime.SpecifyKind(airdate.Value, DateTimeKind.Unspecified);
                            airdateLocal = TimeZoneInfo.ConvertTime(airdateLocal,
                                TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"), TimeZoneInfo.Local);
                        }

                        lock (daysofweekcounter)
                        {
                            if (!daysofweekcounter.ContainsKey(airdateLocal.DayOfWeek))
                                daysofweekcounter.Add(airdateLocal.DayOfWeek, 0);

                            daysofweekcounter[airdateLocal.DayOfWeek]++;
                        }

                        if (lastEpAirDate == null || lastEpAirDate < airdate) lastEpAirDate = airdate.Value;
                    }

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
                        lock (epReleasedList)
                            epReleasedList.Add(ep, !epReleased || vids.Any());
                        lock (epGroupReleasedList)
                            epGroupReleasedList.Add(ep, !epReleasedGroup || vids.Any());
                    }
                    catch (Exception e)
                    {
                        logger.Trace($"Error updating release group stats {e}");
                        throw;
                    }
                });

                foreach (EpisodeList.StatEpisodes eplst in epReleasedList)
                    if (!eplst.Available)
                        MissingEpisodeCount++;
                foreach (EpisodeList.StatEpisodes eplst in epGroupReleasedList)
                    if (!eplst.Available)
                        MissingEpisodeCountGroups++;

                LatestLocalEpisodeNumber = latestLocalEpNumber;
                if (daysofweekcounter.Count > 0)
                    AirsOn = daysofweekcounter.OrderByDescending(a => a.Value).FirstOrDefault().Key;
                LatestEpisodeAirDate = lastEpAirDate;
            }

            ts = DateTime.Now - start;
            logger.Trace($"Updated MISSING EPS stats for SERIES {name} in {ts.TotalMilliseconds}ms");
            start = DateTime.Now;

            // Skip group filters if we are doing group stats, as the group stats will regenerate group filters
            RepoFactory.AnimeSeries.Save(this, updateGroups: false, onlyupdatestats: false, skipgroupfilters: updateAllGroupsAbove);
            ts = DateTime.Now - start;
            logger.Trace($"Saved stats for SERIES {name} in {ts.TotalMilliseconds}ms");

            if (updateAllGroupsAbove)
            {
                start = DateTime.Now;
                AnimeGroup?.TopLevelAnimeGroup?.UpdateStatsFromTopLevel(true, watchedStats, missingEpsStats);
                ts = DateTime.Now - start;
                logger.Trace($"Updated group stats for SERIES {name} in {ts.TotalMilliseconds}ms");
            }

            ts = DateTime.Now - initialStart;
            logger.Trace($"Finished updating stats for SERIES {name} in {ts.TotalSeconds}s");
        }

        public static Dictionary<SVR_AnimeSeries, CrossRef_Anime_Staff> SearchSeriesByStaff(string staffname, bool fuzzy = false)
        {
            var allseries = RepoFactory.AnimeSeries.GetAll();
            var results = new Dictionary<SVR_AnimeSeries, CrossRef_Anime_Staff>();
            List<string> stringsToSearchFor = new List<string> ();
            if (staffname.Contains(" "))
            {
                stringsToSearchFor.AddRange(staffname.Split(' ').GetPermutations()
                    .Select(permutation => string.Join(" ", permutation)));
                stringsToSearchFor.Remove(staffname);
                stringsToSearchFor.Insert(0, staffname);
            }
            else
                stringsToSearchFor.Add(staffname);

            foreach (SVR_AnimeSeries series in allseries)
            {
                List<(CrossRef_Anime_Staff, AnimeStaff)> staff = RepoFactory.CrossRef_Anime_Staff
                    .GetByAnimeID(series.AniDB_ID).Select(a => (a, RepoFactory.AnimeStaff.GetByID(a.StaffID))).ToList();

                foreach (var animeStaff in staff)
                    foreach (var search in stringsToSearchFor)
                    {
                        if (fuzzy)
                        {
                            if (!animeStaff.Item2.Name.FuzzyMatches(search)) continue;
                        }
                        else
                        {
                            if (!animeStaff.Item2.Name.Equals(search, StringComparison.InvariantCultureIgnoreCase)) continue;
                        }

                        if (!results.ContainsKey(series))
                            results.Add(series, animeStaff.Item1);
                        else
                        {
                            if (!Enum.TryParse(results[series].Role, out CharacterAppearanceType type1)) continue;
                            if (!Enum.TryParse(animeStaff.Item1.Role, out CharacterAppearanceType type2)) continue;
                            int comparison = ((int) type1).CompareTo((int) type2);
                            if (comparison == 1) results[series] = animeStaff.Item1;
                        }
                        goto label0;
                    }
                // People hate goto, but this is a legit use for it.
                label0: ;
            }
            return results;
        }

        public void DeleteSeries(bool deleteFiles, bool updateGroups)
        {
            GetAnimeEpisodes().ForEach(ep =>
            {
                ep.RemoveVideoLocals(deleteFiles);
                RepoFactory.AnimeEpisode.Delete(ep.AnimeEpisodeID);
            });
            RepoFactory.AnimeSeries.Delete(this);

            if (!updateGroups) return;
            // finally update stats
            SVR_AnimeGroup grp = AnimeGroup;
            if (grp != null)
            {
                if (!grp.GetAllSeries().Any())
                {
                    // Find the topmost group without series
                    var parent = grp;
                    while (true)
                    {
                        var next = parent.Parent;
                        if (next == null || next.GetAllSeries().Any()) break;
                        parent = next;
                    }
                    parent.DeleteGroup();
                }
                else
                {
                    grp.UpdateStatsFromTopLevel(true, true, true);
                }
            }
        }
    }
}
