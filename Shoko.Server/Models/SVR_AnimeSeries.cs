using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.RegularExpressions;
using Force.DeepCloner;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.LZ4;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Repos;

namespace Shoko.Server.Models
{
    public class SVR_AnimeSeries : AnimeSeries
    {
        public const int CONTRACT_VERSION = 8;
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();


        private CL_AnimeSeries_User _contract;

        [NotMapped]
        public virtual CL_AnimeSeries_User Contract
        {
            get
            {
                if (_contract == null && ContractBlob != null && ContractBlob.Length > 0 && ContractSize > 0)
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

        [NotMapped]
        public string Year => GetAnime().GetYear();

        [NotMapped]
        public string GenresRaw
        {
            get
            {
                if (GetAnime() == null)
                    return string.Empty;
                return GetAnime().TagsString;
            }
        }

        [NotMapped]
        public CrossRef_AniDB_Other CrossRefMovieDB => Repo.CrossRef_AniDB_Other.GetByAnimeIDAndType(AniDB_ID, CrossRefType.MovieDB);

        [NotMapped]
        public List<CrossRef_AniDB_MAL> CrossRefMAL => Repo.CrossRef_AniDB_MAL.GetByAnimeID(AniDB_ID);

        [NotMapped]
        public DateTime AirDate
        {
            get
            {
                var anime = GetAnime();
                if (anime?.AirDate != null)
                    return anime.AirDate.Value;
                // This will be slower, but hopefully more accurate
                DateTime? ep = GetAnimeEpisodes().Select(a => a.AniDB_Episode.GetAirDateAsDate()).Where(a => a != null).OrderBy(a => a).FirstOrDefault();
                if (ep != null)
                    return ep.Value;
                return DateTime.MinValue;
            }
        }

        [NotMapped]
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
        [NotMapped]
        public SVR_AnimeGroup AnimeGroup => Repo.AnimeGroup.GetByID(AnimeGroupID);

        /// <summary>
        ///     Gets the very top level AnimeGroup which this series belongs to
        /// </summary>
        [NotMapped]
        public SVR_AnimeGroup TopLevelAnimeGroup
        {
            get
            {
                SVR_AnimeGroup parentGroup = Repo.AnimeGroup.GetByID(AnimeGroupID);

                while (parentGroup?.AnimeGroupParentID != null)
                {
                    parentGroup = Repo.AnimeGroup.GetByID(parentGroup.AnimeGroupParentID.Value);
                }

                return parentGroup;
            }
        }

        [NotMapped]
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
                        SVR_AnimeGroup grp = Repo.AnimeGroup.GetByID(groupID.Value);
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

        public void CollectContractMemory()
        {
            _contract = null;
        }


        public string GetSeriesName()
        {
            string seriesName;
            if (!string.IsNullOrEmpty(SeriesNameOverride))
                seriesName = SeriesNameOverride;
            else
            {
                if (ServerSettings.SeriesNameSource == DataSourceType.AniDB)
                    seriesName = GetAnime().GetFormattedTitle();
                else
                {
                    List<TvDB_Series> tvdbs = GetTvDBSeries();

                    if (tvdbs != null && tvdbs.Count > 0 && !string.IsNullOrEmpty(tvdbs[0].SeriesName) && !tvdbs[0].SeriesName.ToUpper().Contains("**DUPLICATE"))
                        seriesName = tvdbs[0].SeriesName;
                    else
                        seriesName = GetAnime().GetFormattedTitle();
                }
            }

            return seriesName;
        }

        public string GetFormattedTitle(List<CL_AnimeTitle> titles)
        {
            foreach (NamingLanguage nlan in Languages.PreferredNamingLanguages)
            {
                string thisLanguage = nlan.Language.Trim().ToUpper();

                // Romaji and English titles will be contained in MAIN and/or OFFICIAL
                // we won't use synonyms for these two languages
                if (thisLanguage.Equals(Shoko.Models.Constants.AniDBLanguageType.Romaji) || thisLanguage.Equals(Shoko.Models.Constants.AniDBLanguageType.English))
                {
                    foreach (CL_AnimeTitle title in titles)
                    {
                        string titleType = title.TitleType.Trim().ToUpper();
                        // first try the  Main title
                        if (titleType == Shoko.Models.Constants.AnimeTitleType.Main.ToUpper() && title.Language.Trim().ToUpper() == thisLanguage)
                            return title.Title;
                    }
                }

                // now try the official title
                foreach (CL_AnimeTitle title in titles)
                {
                    string titleType = title.TitleType.Trim().ToUpper();
                    if (titleType == Shoko.Models.Constants.AnimeTitleType.Official.ToUpper() && title.Language.Trim().ToUpper() == thisLanguage)
                        return title.Title;
                }

                // try synonyms
                if (ServerSettings.LanguageUseSynonyms)
                {
                    foreach (CL_AnimeTitle title in titles)
                    {
                        string titleType = title.TitleType.Trim().ToUpper();
                        if (titleType == Shoko.Models.Constants.AnimeTitleType.Synonym.ToUpper() && title.Language.Trim().ToUpper() == thisLanguage)
                            return title.Title;
                    }
                }
            }

            return null;
        }

        public string GetSeriesNameFromContract(CL_AnimeSeries_User con)
        {
            if (!string.IsNullOrEmpty(con.SeriesNameOverride))
                return SeriesNameOverride;
            if (ServerSettings.SeriesNameSource != DataSourceType.AniDB)
            {
                if (con.TvDB_Series != null && con.TvDB_Series.Count > 0 && !string.IsNullOrEmpty(con.TvDB_Series[0].SeriesName) && !con.TvDB_Series[0].SeriesName.ToUpper().Contains("**DUPLICATE"))
                {
                    return con.TvDB_Series[0].SeriesName;
                }
            }

            return GetFormattedTitle(con.AniDBAnime.AnimeTitles) ?? con.AniDBAnime.AniDBAnime.MainTitle;
        }


        public List<SVR_AnimeEpisode> GetAnimeEpisodes() => Repo.AnimeEpisode.GetBySeriesID(AnimeSeriesID);

        public int GetAnimeEpisodesNormalCountWithVideoLocal()
        {
            return Repo.AnimeEpisode.GetBySeriesID(AnimeSeriesID).Count(a => a.EpisodeTypeEnum == EpisodeType.Episode && Repo.CrossRef_File_Episode.GetByEpisodeID(Repo.AniDB_Episode.GetByEpisodeID(a.AniDB_EpisodeID)?.EpisodeID ?? 0).Select(b => Repo.VideoLocal.GetByHash(b.Hash)).Count(b => b != null) > 0);

            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return
                    Convert.ToInt32(
                        session.CreateQuery(
                            "Select count(*) FROM AnimeEpisode as aepi, AniDB_Episode as epi WHERE aepi.AniDB_EpisodeID = epi.EpisodeID AND epi.EpisodeType=1 AND (select count(*) from VideoLocal as vl, CrossRef_File_Episode as xref where vl.Hash = xref.Hash and xref.EpisodeID = epi.EpisodeID) > 0 AND aepi.AnimeSeriesID = :animeid")
                            .SetParameter("animeid", AnimeSeriesID)
                            .UniqueResult());
            }*/
        }

        public int GetAnimeNumberOfEpisodeTypes()
        {
            return Repo.AnimeEpisode.GetBySeriesID(AnimeSeriesID).Where(a => Repo.CrossRef_File_Episode.GetByEpisodeID(Repo.AniDB_Episode.GetByEpisodeID(a.AniDB_EpisodeID)?.EpisodeID ?? 0).Select(b => Repo.VideoLocal.GetByHash(b.Hash)).Count(b => b != null) > 0).Select(a => a.EpisodeTypeEnum).Distinct().Count();
            /*


            return
                new AnimeEpisodeRepository()
                    .GetBySeriesID(AnimeSeriesID)
                    .Select(a => aer.GetByEpisodeID(a.AniDB_EpisodeID))
                    .Where(a => a != null)
                    .SelectMany(a => cr.GetByEpisodeID(a.EpisodeID))
                    .Where(a => a != null && vl.GetByHash(a.Hash) != null).GroupBy(a=>a.);*/
            /*
             * 
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return
                    Convert.ToInt32(
                        session.CreateQuery(
                            "Select count(distinct epi.EpisodeType) FROM AnimeEpisode as aepi, AniDB_Episode as epi WHERE aepi.AniDB_EpisodeID = epi.EpisodeID AND epi.EpisodeType=1 AND (select count(*) from VideoLocal as vl, CrossRef_File_Episode as xref where vl.Hash = xref.Hash and xref.EpisodeID = epi.EpisodeID) > 0 AND aepi.AnimeSeriesID = :animeid")
                            .SetParameter("animeid", AnimeSeriesID)
                            .UniqueResult());
            }*/
        }

        public int GetAnimeEpisodesCountWithVideoLocal()
        {
            return Repo.AnimeEpisode.GetBySeriesID(AnimeSeriesID).Count(a => Repo.CrossRef_File_Episode.GetByEpisodeID(Repo.AniDB_Episode.GetByEpisodeID(a.AniDB_EpisodeID)?.EpisodeID ?? 0).Select(b => Repo.VideoLocal.GetByHash(b.Hash)).Count(b => b != null) > 0);
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return
                    Convert.ToInt32(
                        session.CreateQuery(
                            "Select count(*) FROM AnimeEpisode as aepi, AniDB_Episode as epi WHERE aepi.AniDB_EpisodeID = epi.EpisodeID AND (select count(*) from VideoLocal as vl, CrossRef_File_Episode as xref where vl.Hash = xref.Hash and xref.EpisodeID = epi.EpisodeID) > 0 AND aepi.AnimeSeriesID = :animeid")
                            .SetParameter("animeid", AnimeSeriesID)
                            .UniqueResult());
            }*/
        }

        public CL_AnimeSeries_User GetUserContract(int userid, HashSet<GroupFilterConditionType> types = null)
        {
            CL_AnimeSeries_User contract = Contract.DeepClone();
            SVR_AnimeSeries_User rr = GetUserRecord(userid);
            if (rr != null)
            {
                contract.UnwatchedEpisodeCount = rr.UnwatchedEpisodeCount;
                contract.WatchedEpisodeCount = rr.WatchedEpisodeCount;
                contract.WatchedDate = rr.WatchedDate;
                contract.PlayedCount = rr.PlayedCount;
                contract.WatchedCount = rr.WatchedCount;
                contract.StoppedCount = rr.StoppedCount;
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

            contract.AniDBAnime.AniDBAnime.FormattedTitle = GetSeriesNameFromContract(contract);
            return contract;
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
            if (rr == null)
            {
                using (var upd = Repo.AnimeSeries_User.BeginAdd())
                {
                    upd.Entity.WatchedCount = 0;
                    upd.Entity.UnwatchedEpisodeCount = 0;
                    upd.Entity.PlayedCount = 0;
                    upd.Entity.StoppedCount = 0;
                    upd.Entity.WatchedEpisodeCount = 0;
                    upd.Entity.WatchedDate = null;
                    rr = upd.Commit();
                }
            }

            return rr;
        }

        public SVR_AnimeEpisode GetLastEpisodeWatched(int userID)
        {
            SVR_AnimeEpisode watchedep = null;
            SVR_AnimeEpisode_User userRecordWatched = null;

            foreach (SVR_AnimeEpisode ep in GetAnimeEpisodes())
            {
                SVR_AnimeEpisode_User userRecord = ep.GetUserRecord(userID);
                if (userRecord != null && ep.EpisodeTypeEnum == EpisodeType.Episode)
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

        public SVR_AnimeSeries_User GetUserRecord(int userID) => Repo.AnimeSeries_User.GetByUserAndSeriesID(userID, AnimeSeriesID);


        public SVR_AniDB_Anime GetAnime() => Repo.AniDB_Anime.GetByID(AniDB_ID);

        public void Populate_RA(SVR_AniDB_Anime anime)
        {
            AniDB_ID = anime.AnimeID;
            LatestLocalEpisodeNumber = 0;
            DateTimeUpdated = DateTime.Now;
            DateTimeCreated = DateTime.Now;
            SeriesNameOverride = string.Empty;
        }


        public void CreateAnimeEpisodes()
        {
            SVR_AniDB_Anime anime = GetAnime();
            if (anime == null) return;

            foreach (AniDB_Episode ep in anime.GetAniDBEpisodes())
            {
                ep.CreateAnimeEpisode(AnimeSeriesID);
            }
        }

        public void UpdateGroupFilters(HashSet<GroupFilterConditionType> types, SVR_JMMUser user = null)
        {
            HashSet<GroupFilterConditionType> n = new HashSet<GroupFilterConditionType>(types);
            IEnumerable<SVR_JMMUser> users = new List<SVR_JMMUser> {user};
            if (user == null)
                users = Repo.JMMUser.GetAll();
            Dictionary<int, CL_AnimeSeries_User> cgrps = new Dictionary<int, CL_AnimeSeries_User>();
            foreach (SVR_JMMUser u in users)
            {
                cgrps.Add(u.JMMUserID, GetUserContract(u.JMMUserID, n));
            }
            using (var upd = Repo.GroupFilter.BeginBatchUpdate(() => Repo.GroupFilter.GetWithConditionTypesAndAll(n)))
            {
                foreach (SVR_GroupFilter gf in upd)
                {
                    foreach (SVR_JMMUser u in users)
                        gf.CalculateGroupFilterSeries_RA(cgrps[u.JMMUserID], u, u.JMMUserID);
                    upd.Update(gf);
                }
                upd.Commit();
            }
        }


        public void DeleteFromFilters()
        {
            using (var upd = Repo.GroupFilter.BeginBatchUpdate(() => Repo.GroupFilter.GetAll()))
            {
                foreach (SVR_GroupFilter gf in upd)
                {
                    if (gf.SeriesIds.Values.SelectMany(a => a).Contains(AnimeSeriesID))
                    {
                        foreach (int k in gf.SeriesIds.Keys)
                        {
                            if (gf.GroupsIds[k].Contains(AnimeSeriesID))
                                gf.GroupsIds[k].Remove(AnimeSeriesID);
                        }
                        upd.Update(gf);
                    }
                }
                upd.Commit();
            }

        }


        public static Dictionary<int, HashSet<GroupFilterConditionType>> BatchUpdateContracts(ICollection<SVR_AnimeSeries> seriesBatch, bool onlyStats = false)
        {
            if (seriesBatch == null)
                throw new ArgumentNullException(nameof(seriesBatch));

            var grpFilterCondTypesPerSeries = new Dictionary<int, HashSet<GroupFilterConditionType>>();

            if (seriesBatch.Count == 0)
            {
                return grpFilterCondTypesPerSeries;
            }

            var animeIds = seriesBatch.Select(s => s.AniDB_ID).ToList();
            var tvDbByAnime = Repo.TvDB_Series.GetByAnimeIDsV2(animeIds);
            var movieByAnime = Repo.MovieDb_Movie.GetByAnimeIDs(animeIds);
            var malXrefByAnime = Repo.CrossRef_AniDB_MAL.GetByAnimeIDs(animeIds);
            var defImagesByAnime = Repo.AniDB_Anime.GetDefaultImagesByAnime(animeIds);

            foreach (SVR_AnimeSeries oa in seriesBatch)
            {
                SVR_AnimeSeries series = oa;
                CL_AnimeSeries_User contract = series.Contract?.DeepClone();
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
                        contract.AniDBAnime = animeRec.Contract.DeepClone();

                        CL_AniDB_Anime aniDbAnime = contract.AniDBAnime.AniDBAnime;

                        if (!defImagesByAnime.TryGetValue(animeRec.AnimeID, out DefaultAnimeImages defImages))
                        {
                            defImages = new DefaultAnimeImages {AnimeID = animeRec.AnimeID};
                        }

                        aniDbAnime.DefaultImagePoster = defImages.GetPosterContractNoBlanks();
                        aniDbAnime.DefaultImageFanart = defImages.GetFanartContractNoBlanks(aniDbAnime);
                        aniDbAnime.DefaultImageWideBanner = defImages.WideBanner?.ToContract();
                    }

                    // TvDB contracts
                    var tvDbCrossRefs = tvDbByAnime[series.AniDB_ID].ToList();

                    foreach (var missingTvDbSeries in tvDbCrossRefs.Where(cr => cr.Item2 == null).Select(cr => cr.Item1))
                    {
                        logger.Warn("You are missing database information for TvDB series: {0} - {1}", missingTvDbSeries.TvDBID, missingTvDbSeries.TvDBTitle);
                    }

                    contract.CrossRefAniDBTvDBV2 = tvDbCrossRefs.Select(s => s.Item1).ToList();
                    contract.TvDB_Series = tvDbCrossRefs.Select(s => s.Item2).ToList();

                    // MovieDB contracts

                    if (movieByAnime.TryGetValue(series.AniDB_ID, out (CrossRef_AniDB_Other, MovieDB_Movie) movieDbInfo))
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
                    contract.CrossRefAniDBMAL = malXrefByAnime[series.AniDB_ID].ToList();
                }

                HashSet<GroupFilterConditionType> typesChanged = GetConditionTypesChanged(series.Contract, contract);
                using (var upd = Repo.AnimeSeries.BeginAddOrUpdate(()=>Repo.AnimeSeries.GetByID(series.AnimeSeriesID)))
                {
                    upd.Entity.Contract = contract;
                    series=upd.Commit();
                }
                grpFilterCondTypesPerSeries.Add(series.AnimeSeriesID, typesChanged);
            }

            return grpFilterCondTypesPerSeries;
        }

        public static SVR_AnimeSeries UpdateContract(int animeseriesId, bool onlystats = false)
        {
            using (var upd = Repo.AnimeSeries.BeginAddOrUpdate(() => Repo.AnimeSeries.GetByID(animeseriesId)))
            {
                upd.Entity.Contract = upd.Entity.GenerateContract(onlystats).Contract;
                return upd.Commit();
            }
        }
        public (CL_AnimeSeries_User Contract, HashSet<GroupFilterConditionType> Conditions) GenerateContract(bool onlystats = false)
        {
            CL_AnimeSeries_User contract = Contract?.DeepClone();
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
                HashSet<GroupFilterConditionType> types2 = GetConditionTypesChanged(Contract, contract);
                return (contract,types2);
            }

            SVR_AniDB_Anime animeRec = GetAnime();
            List<CrossRef_AniDB_TvDBV2> tvDBCrossRefs = GetCrossRefTvDBV2();
            CrossRef_AniDB_Other movieDBCrossRef = CrossRefMovieDB;
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
                    logger.Warn("You are missing database information for TvDB series: {0} - {1}", xref.TvDBID, xref.TvDBTitle);
            }

            // get AniDB data
            if (animeRec != null)
            {
                if (animeRec.Contract == null)
                {
                    SVR_AniDB_Anime.UpdateContractDetailed(animeRec);
                }

                contract.AniDBAnime = animeRec.Contract.DeepClone();
                contract.AniDBAnime.AniDBAnime.DefaultImagePoster = animeRec.GetDefaultPoster()?.ToClient();
                if (contract.AniDBAnime.AniDBAnime.DefaultImagePoster == null)
                {
                    ImageDetails im = animeRec.GetDefaultPosterDetailsNoBlanks();
                    if (im != null)
                    {
                        contract.AniDBAnime.AniDBAnime.DefaultImagePoster = new CL_AniDB_Anime_DefaultImage
                        {
                            AnimeID = im.ImageID,
                            ImageType = (int)im.ImageType
                        };
                    }
                }

                contract.AniDBAnime.AniDBAnime.DefaultImageFanart = animeRec.GetDefaultFanart()?.ToClient();
                if (contract.AniDBAnime.AniDBAnime.DefaultImageFanart == null)
                {
                    ImageDetails im = animeRec.GetDefaultFanartDetailsNoBlanks();
                    if (im != null)
                    {
                        contract.AniDBAnime.AniDBAnime.DefaultImageFanart = new CL_AniDB_Anime_DefaultImage
                        {
                            AnimeID = im.ImageID,
                            ImageType = (int)im.ImageType
                        };
                    }
                }

                contract.AniDBAnime.AniDBAnime.DefaultImageWideBanner = animeRec.GetDefaultWideBanner()?.ToClient();
            }

            contract.CrossRefAniDBTvDBV2 = tvDBCrossRefs.ToList();


            contract.TvDB_Series = sers;
            contract.CrossRefAniDBMovieDB = null;
            if (movieDBCrossRef != null)
            {
                contract.CrossRefAniDBMovieDB = movieDBCrossRef;
                contract.MovieDB_Movie = movie;
            }

            contract.CrossRefAniDBMAL = CrossRefMAL?.ToList() ?? new List<CrossRef_AniDB_MAL>();
            HashSet<GroupFilterConditionType> types = GetConditionTypesChanged(Contract, contract);
            return (contract,types);

        }


        public static HashSet<GroupFilterConditionType> GetConditionTypesChanged(CL_AnimeSeries_User oldcontract, CL_AnimeSeries_User newcontract)
        {
            HashSet<GroupFilterConditionType> h = new HashSet<GroupFilterConditionType>();

            if (oldcontract == null || (oldcontract.AniDBAnime.AniDBAnime.EndDate.HasValue && oldcontract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now && !(oldcontract.MissingEpisodeCount > 0 || oldcontract.MissingEpisodeCountGroups > 0)) != (newcontract.AniDBAnime.AniDBAnime.EndDate.HasValue && newcontract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now && !(newcontract.MissingEpisodeCount > 0 || newcontract.MissingEpisodeCountGroups > 0)))
                h.Add(GroupFilterConditionType.CompletedSeries);
            if (oldcontract == null || (oldcontract.MissingEpisodeCount > 0 || oldcontract.MissingEpisodeCountGroups > 0) != (newcontract.MissingEpisodeCount > 0 || newcontract.MissingEpisodeCountGroups > 0))
                h.Add(GroupFilterConditionType.MissingEpisodes);
            if (oldcontract == null || !oldcontract.AniDBAnime.AniDBAnime.GetAllTags().SetEquals(newcontract.AniDBAnime.AniDBAnime.GetAllTags()))
                h.Add(GroupFilterConditionType.Tag);
            if (oldcontract == null || oldcontract.AniDBAnime.AniDBAnime.AirDate != newcontract.AniDBAnime.AniDBAnime.AirDate)
                h.Add(GroupFilterConditionType.AirDate);
            if (oldcontract == null || (oldcontract.CrossRefAniDBTvDBV2 == null || oldcontract.CrossRefAniDBTvDBV2.Count == 0) != (newcontract.CrossRefAniDBTvDBV2 == null || newcontract.CrossRefAniDBTvDBV2.Count == 0))
                h.Add(GroupFilterConditionType.AssignedTvDBInfo);
            if (oldcontract == null || (oldcontract.CrossRefAniDBMAL == null || oldcontract.CrossRefAniDBMAL.Count == 0) != (newcontract.CrossRefAniDBMAL == null || newcontract.CrossRefAniDBMAL.Count == 0))
                h.Add(GroupFilterConditionType.AssignedMALInfo);
            if (oldcontract == null || oldcontract.CrossRefAniDBMovieDB == null != (newcontract.CrossRefAniDBMovieDB == null))
                h.Add(GroupFilterConditionType.AssignedMovieDBInfo);
            if (oldcontract == null || oldcontract.CrossRefAniDBMovieDB == null && (oldcontract.CrossRefAniDBTvDBV2 == null || oldcontract.CrossRefAniDBTvDBV2.Count == 0) != (newcontract.CrossRefAniDBMovieDB == null && (newcontract.CrossRefAniDBTvDBV2 == null || newcontract.CrossRefAniDBTvDBV2.Count == 0)))
                h.Add(GroupFilterConditionType.AssignedTvDBOrMovieDBInfo);
            if (oldcontract == null || oldcontract.AniDBAnime.AniDBAnime.AnimeType != newcontract.AniDBAnime.AniDBAnime.AnimeType)
                h.Add(GroupFilterConditionType.AnimeType);
            if (oldcontract == null || !oldcontract.AniDBAnime.Stat_AllVideoQuality.SetEquals(newcontract.AniDBAnime.Stat_AllVideoQuality) || !oldcontract.AniDBAnime.Stat_AllVideoQuality_Episodes.SetEquals(newcontract.AniDBAnime.Stat_AllVideoQuality_Episodes))
                h.Add(GroupFilterConditionType.VideoQuality);
            if (oldcontract == null || oldcontract.AniDBAnime.AniDBAnime.VoteCount != newcontract.AniDBAnime.AniDBAnime.VoteCount || oldcontract.AniDBAnime.AniDBAnime.TempVoteCount != newcontract.AniDBAnime.AniDBAnime.TempVoteCount || oldcontract.AniDBAnime.AniDBAnime.Rating != newcontract.AniDBAnime.AniDBAnime.Rating || oldcontract.AniDBAnime.AniDBAnime.TempRating != newcontract.AniDBAnime.AniDBAnime.TempRating)
                h.Add(GroupFilterConditionType.AniDBRating);
            if (oldcontract == null || oldcontract.DateTimeCreated != newcontract.DateTimeCreated)
                h.Add(GroupFilterConditionType.SeriesCreatedDate);
            if (oldcontract == null || oldcontract.EpisodeAddedDate != newcontract.EpisodeAddedDate)
                h.Add(GroupFilterConditionType.EpisodeAddedDate);
            if (oldcontract == null || (oldcontract.AniDBAnime.AniDBAnime.EndDate.HasValue && oldcontract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now) != (newcontract.AniDBAnime.AniDBAnime.EndDate.HasValue && newcontract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now))
                h.Add(GroupFilterConditionType.FinishedAiring);
            if (oldcontract == null || oldcontract.MissingEpisodeCountGroups > 0 != newcontract.MissingEpisodeCountGroups > 0)
                h.Add(GroupFilterConditionType.MissingEpisodesCollecting);
            if (oldcontract == null || !oldcontract.AniDBAnime.Stat_AudioLanguages.SetEquals(newcontract.AniDBAnime.Stat_AudioLanguages))
                h.Add(GroupFilterConditionType.AudioLanguage);
            if (oldcontract == null || !oldcontract.AniDBAnime.Stat_SubtitleLanguages.SetEquals(newcontract.AniDBAnime.Stat_SubtitleLanguages))
                h.Add(GroupFilterConditionType.SubtitleLanguage);
            if (oldcontract == null || oldcontract.AniDBAnime.AniDBAnime.EpisodeCount != newcontract.AniDBAnime.AniDBAnime.EpisodeCount)
                h.Add(GroupFilterConditionType.EpisodeCount);
            if (oldcontract == null || !oldcontract.AniDBAnime.CustomTags.Select(a => a.TagName).ToHashSet().SetEquals(newcontract.AniDBAnime.CustomTags.Select(a => a.TagName).ToHashSet()))
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
            if (oldcontract == null || (oldcontract.AniDBAnime.UserVote != null && oldcontract.AniDBAnime.UserVote.VoteType == (int) AniDBVoteType.Anime) != (newcontract.AniDBAnime.UserVote != null && newcontract.AniDBAnime.UserVote.VoteType == (int) AniDBVoteType.Anime))
                h.Add(GroupFilterConditionType.UserVoted);
            if (oldcontract == null || oldcontract.AniDBAnime.UserVote != null != (newcontract.AniDBAnime.UserVote != null))
                h.Add(GroupFilterConditionType.UserVotedAny);
            if (oldcontract == null || (oldcontract.AniDBAnime.UserVote?.VoteValue ?? 0) != (newcontract.AniDBAnime.UserVote?.VoteValue ?? 0))
                h.Add(GroupFilterConditionType.UserRating);

            return h;
        }

        public override string ToString() => $"Series: {GetAnime().MainTitle} ({AnimeSeriesID})";

        public void QueueUpdateStats()
        {
            CommandRequest_RefreshAnime cmdRefreshAnime = new CommandRequest_RefreshAnime(AniDB_ID);
            cmdRefreshAnime.Save();
        }

        public static SVR_AnimeSeries UpdateStats(SVR_AnimeSeries animeSeries, bool watchedStats, bool missingEpsStats, bool updateAllGroupsAbove)
        {
            DateTime start = DateTime.Now;
            //DateTime startOverall = DateTime.Now;
            logger.Info("Starting Updating STATS for SERIES {0} ({1} - {2} - {3})", animeSeries, watchedStats, missingEpsStats, updateAllGroupsAbove);


            List<SVR_JMMUser> allUsers = Repo.JMMUser.GetAll();

            DateTime startEps = DateTime.Now;
            List<SVR_AnimeEpisode> eps = animeSeries.GetAnimeEpisodes();
            TimeSpan tsEps = DateTime.Now - startEps;
            logger.Trace("Got episodes for SERIES {0} in {1}ms", animeSeries, tsEps.TotalMilliseconds);

            DateTime startVids = DateTime.Now;
            List<SVR_VideoLocal> vidsTemp = Repo.VideoLocal.GetByAniDBAnimeID(animeSeries.AniDB_ID);
            List<CrossRef_File_Episode> crossRefs = Repo.CrossRef_File_Episode.GetByAnimeID(animeSeries.AniDB_ID);

            Dictionary<int, List<CrossRef_File_Episode>> dictCrossRefs = new Dictionary<int, List<CrossRef_File_Episode>>();
            foreach (CrossRef_File_Episode xref in crossRefs)
            {
                if (!dictCrossRefs.ContainsKey(xref.EpisodeID))
                    dictCrossRefs[xref.EpisodeID] = new List<CrossRef_File_Episode>();
                dictCrossRefs[xref.EpisodeID].Add(xref);
            }

            Dictionary<string, SVR_VideoLocal> dictVids = new Dictionary<string, SVR_VideoLocal>();
            foreach (SVR_VideoLocal vid in vidsTemp)
            {
                //Hashes may be repeated from multiple locations but we don't care
                dictVids[vid.Hash] = vid;
            }

            TimeSpan tsVids = DateTime.Now - startVids;
            logger.Trace("Got video locals for SERIES {0} in {1}ms", animeSeries, tsVids.TotalMilliseconds);


            if (watchedStats)
            {
                using (var suupd = Repo.AnimeSeries_User.BeginBatchUpdate(() => Repo.AnimeSeries_User.GetBySeriesID(animeSeries.AnimeSeriesID)))
                {
                    foreach (SVR_JMMUser juser in allUsers)
                    {
                        SVR_AnimeSeries_User userRecord = suupd.FindOrCreate(a => a.JMMUserID == juser.JMMUserID);

                        userRecord.JMMUserID = juser.JMMUserID;
                        userRecord.AnimeSeriesID = animeSeries.AnimeSeriesID;
                        // reset stats
                        userRecord.UnwatchedEpisodeCount = 0;
                        userRecord.WatchedEpisodeCount = 0;
                        userRecord.WatchedCount = 0;
                        userRecord.WatchedDate = null;

                        DateTime startUser = DateTime.Now;
                        List<SVR_AnimeEpisode_User> epUserRecords = Repo.AnimeEpisode_User.GetByUserID(juser.JMMUserID);
                        Dictionary<int, SVR_AnimeEpisode_User> dictUserRecords = new Dictionary<int, SVR_AnimeEpisode_User>();
                        foreach (SVR_AnimeEpisode_User usrec in epUserRecords)
                            dictUserRecords[usrec.AnimeEpisodeID] = usrec;
                        TimeSpan tsUser = DateTime.Now - startUser;
                        logger.Trace("Got user records for SERIES {0}/{1} in {2}ms", animeSeries, juser.Username, tsUser.TotalMilliseconds);

                        foreach (SVR_AnimeEpisode ep in eps)
                        {
                            // if the episode doesn't have any files then it won't count towards watched/unwatched counts
                            List<SVR_VideoLocal> epVids = new List<SVR_VideoLocal>();

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

                            if (ep.EpisodeTypeEnum == EpisodeType.Episode || ep.EpisodeTypeEnum == EpisodeType.Special)
                            {
                                SVR_AnimeEpisode_User epUserRecord = null;
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
                        suupd.Update(userRecord);
                    }
                    suupd.Commit();
                }
            }

                TimeSpan ts = DateTime.Now - start;
                logger.Trace("Updated WATCHED stats for SERIES {0} in {1}ms", animeSeries, ts.TotalMilliseconds);
                start = DateTime.Now;


                if (missingEpsStats)
                {
                    AnimeType animeType = AnimeType.TVSeries;
                    SVR_AniDB_Anime aniDB_Anime = animeSeries.GetAnime();
                    if (aniDB_Anime != null)
                    {
                        animeType = aniDB_Anime.GetAnimeTypeEnum();
                    }

                    int missingEpisodeCount = 0;
                    int missingEpisodeCountGroups = 0;

                    // get all the group status records
                    List<AniDB_GroupStatus> grpStatuses = Repo.AniDB_GroupStatus.GetByAnimeID(animeSeries.AniDB_ID);

                    // find all the episodes for which the user has a file
                    // from this we can determine what their latest episode number is
                    // find out which groups the user is collecting

                    List<int> userReleaseGroups = new List<int>();
                    foreach (SVR_AnimeEpisode ep in eps)
                    {
                        List<SVR_VideoLocal> vids = new List<SVR_VideoLocal>();
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
                        foreach (SVR_VideoLocal vid in vids)
                        {
                            SVR_AniDB_File anifile = vid.GetAniDBFile();
                            if (anifile != null)
                            {
                                if (!userReleaseGroups.Contains(anifile.GroupID)) userReleaseGroups.Add(anifile.GroupID);
                            }
                        }
                    }

                    int latestLocalEpNumber = 0;
                    DateTime lastEpAirDate = DateTime.MinValue;
                    EpisodeList epReleasedList = new EpisodeList(animeType);
                    EpisodeList epGroupReleasedList = new EpisodeList(animeType);

                    Dictionary<DayOfWeek, int> daysofweekcounter = new Dictionary<DayOfWeek, int>();

                    foreach (SVR_AnimeEpisode ep in eps)
                    {
                        //List<VideoLocal> vids = ep.VideoLocals;
                        if (ep.EpisodeTypeEnum != EpisodeType.Episode) continue;

                        List<SVR_VideoLocal> vids = new List<SVR_VideoLocal>();
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
                        {
                            latestLocalEpNumber = thisEpNum;
                        }

                        DateTime? airdate = ep.AniDB_Episode.GetAirDateAsDate();

                        // Only count episodes that have already aired
                        if (airdate.HasValue && !(airdate > DateTime.Now))
                        {
                            // Only convert if we have time info
                            DateTime airdateLocal;
                            if (airdate.Value.Hour == 0 && airdate.Value.Minute == 0 && airdate.Value.Second == 0)
                            {
                                airdateLocal = airdate.Value;
                            }
                            else
                            {
                                airdateLocal = DateTime.SpecifyKind(airdate.Value, DateTimeKind.Unspecified);
                                airdateLocal = TimeZoneInfo.ConvertTime(airdateLocal, TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"), TimeZoneInfo.Local);
                            }

                            if (!daysofweekcounter.ContainsKey(airdateLocal.DayOfWeek))
                                daysofweekcounter.Add(airdateLocal.DayOfWeek, 0);
                            daysofweekcounter[airdateLocal.DayOfWeek]++;
                            if (lastEpAirDate < airdate.Value)
                                lastEpAirDate = airdate.Value;
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
                            epReleasedList.Add(ep, !epReleased || vids.Count != 0);
                            epGroupReleasedList.Add(ep, !epReleasedGroup || vids.Count != 0);
                        }
                        catch (Exception e)
                        {
                            logger.Trace($"Error {e}");
                            throw;
                        }
                    }

                    foreach (EpisodeList.StatEpisodes eplst in epReleasedList)
                    {
                        if (!eplst.Available)
                            missingEpisodeCount++;
                    }

                    foreach (EpisodeList.StatEpisodes eplst in epGroupReleasedList)
                    {
                        if (!eplst.Available)
                            missingEpisodeCountGroups++;
                    }


                    using (var serupd = Repo.AnimeSeries.BeginAddOrUpdate(() => Repo.AnimeSeries.GetByID(animeSeries.AnimeSeriesID)))
                    {
                        serupd.Entity.MissingEpisodeCount = missingEpisodeCount;
                        serupd.Entity.MissingEpisodeCountGroups = missingEpisodeCountGroups;
                        serupd.Entity.LatestLocalEpisodeNumber = latestLocalEpNumber;
                        if (lastEpAirDate != DateTime.MinValue)
                            serupd.Entity.LatestEpisodeAirDate = lastEpAirDate;
                        if (daysofweekcounter.Count > 0)
                        {
                            serupd.Entity.AirsOn = daysofweekcounter.OrderByDescending(a => a.Value).FirstOrDefault().Key;
                        }

                        animeSeries = serupd.Commit((false, false, false, false));
                    }

                }
                
                ts = DateTime.Now - start;
                logger.Trace("Updated MISSING EPS stats for SERIES {0} in {1}ms", animeSeries, ts.TotalMilliseconds);

            

            if (updateAllGroupsAbove)
            {
                foreach (SVR_AnimeGroup grp in animeSeries.AllGroupsAbove)
                {
                    SVR_AnimeGroup.UpdateStats(grp, watchedStats, missingEpsStats);
                }
            }

            return animeSeries;
            /*
            ts = DateTime.Now - start;
	        logger.Trace("Updated GROUPS ABOVE stats for SERIES {0} in {1}ms", this.ToString(), ts.TotalMilliseconds);
	        start = DateTime.Now;

	        TimeSpan tsOverall = DateTime.Now - startOverall;
	        logger.Info("Finished Updating STATS for SERIES {0} in {1}ms ({2} - {3} - {4})", this.ToString(),
	            tsOverall.TotalMilliseconds,
	            watchedStats, missingEpsStats, updateAllGroupsAbove);
                */
//	        StatsCache.Instance.UpdateUsingSeries(AnimeSeriesID);
        }

        internal class EpisodeList : List<EpisodeList.StatEpisodes>
        {
            private readonly Regex partmatch = new Regex("part (\\d.*?) of (\\d.*)");

            private readonly Regex remmultispace = new Regex("\\s+");

            private readonly Regex remsymbols = new Regex("[^A-Za-z0-9 ]");

            public EpisodeList(AnimeType ept)
            {
                AnimeType = ept;
            }

            private AnimeType AnimeType { get; }

            public void Add(SVR_AnimeEpisode ep, bool available)
            {
                if (AnimeType == AnimeType.OVA || AnimeType == AnimeType.Movie)
                {
                    AniDB_Episode aniEp = ep.AniDB_Episode;
                    string ename = aniEp.EnglishName.ToLower();
                    Match m = partmatch.Match(ename);
                    StatEpisodes.StatEpisode s = new StatEpisodes.StatEpisode
                    {
                        Available = available
                    };
                    if (m.Success)
                    {
                        //int.TryParse(m.Groups[1].Value, out int part_number);
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
                        if (ename == "complete movie" || ename == "movie" || ename == "ova")
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
                            if (k.EpisodeType == StatEpisode.EpType.Complete && k.Available)
                                return true;
                            if (k.EpisodeType == StatEpisode.EpType.Part && k.Available)
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

        public int ContractVersion { get; set; }
        public byte[] ContractBlob { get; set; }
        public int ContractSize { get; set; }

        #endregion

        #region TvDB

        public List<CrossRef_AniDB_TvDBV2> GetCrossRefTvDBV2() => Repo.CrossRef_AniDB_TvDBV2.GetByAnimeID(AniDB_ID);

        public List<TvDB_Series> GetTvDBSeries()
        {
            List<TvDB_Series> sers = new List<TvDB_Series>();

            List<CrossRef_AniDB_TvDBV2> xrefs = GetCrossRefTvDBV2();
            if (xrefs == null || xrefs.Count == 0) return sers;

            foreach (CrossRef_AniDB_TvDBV2 xref in xrefs)
                sers.Add(xref.GetTvDBSeries());

            return sers;
        }

        #endregion

        #region Trakt

        public List<CrossRef_AniDB_TraktV2> GetCrossRefTraktV2()
        {
            return Repo.CrossRef_AniDB_TraktV2.GetByAnimeID(AniDB_ID);
        }


        public List<Trakt_Show> GetTraktShow()
        {
            List<Trakt_Show> sers = new List<Trakt_Show>();

            List<CrossRef_AniDB_TraktV2> xrefs = GetCrossRefTraktV2();
            if (xrefs == null || xrefs.Count == 0) return sers;

            foreach (CrossRef_AniDB_TraktV2 xref in xrefs)
                sers.Add(xref.GetByTraktShow());

            return sers;
        }

        #endregion
    }
}