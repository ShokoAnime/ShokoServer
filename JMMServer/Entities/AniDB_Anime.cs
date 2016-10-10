using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using AniDBAPI;

using JMMContracts;
using JMMServer.Collections;
using JMMServer.Commands;
using JMMServer.Databases;
using JMMServer.ImageDownload;
using JMMServer.LZ4;
using JMMServer.Properties;
using JMMServer.Providers.Azure;
using JMMServer.Repositories;
using JMMServer.Repositories.Cached;
using JMMServer.Repositories.Direct;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;
using NLog;

namespace JMMServer.Entities
{
    public class AniDB_Anime
    {
        #region DB columns

        public int AniDB_AnimeID { get; private set; }
        public int AnimeID { get; set; }
        public int EpisodeCount { get; set; }
        public DateTime? AirDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string URL { get; set; }
        public string Picname { get; set; }
        public int BeginYear { get; set; }
        public int EndYear { get; set; }
        public int AnimeType { get; set; }
        public string MainTitle { get; set; }
        public string AllTitles { get; set; }
        public string AllTags { get; set; }
        public string Description { get; set; }
        public int EpisodeCountNormal { get; set; }
        public int EpisodeCountSpecial { get; set; }
        public int Rating { get; set; }
        public int VoteCount { get; set; }
        public int TempRating { get; set; }
        public int TempVoteCount { get; set; }
        public int AvgReviewRating { get; set; }
        public int ReviewCount { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public DateTime DateTimeDescUpdated { get; set; }
        public int ImageEnabled { get; set; }
        public string AwardList { get; set; }
        public int Restricted { get; set; }
        public int? AnimePlanetID { get; set; }
        public int? ANNID { get; set; }
        public int? AllCinemaID { get; set; }
        public int? AnimeNfo { get; set; }
        public int? LatestEpisodeNumber { get; set; }
        public int DisableExternalLinksFlag { get; set; }

        public int ContractVersion { get; set; }
        public byte[] ContractBlob { get; set; }
        public int ContractSize { get; set; }

        public const int CONTRACT_VERSION = 5;


        #endregion

        private Contract_AniDB_AnimeDetailed _contract = null;

        public virtual Contract_AniDB_AnimeDetailed Contract
        {
            get
            {
                if ((_contract == null) && (ContractBlob != null) && (ContractBlob.Length > 0) && (ContractSize > 0))
                    _contract = CompressionHelper.DeserializeObject<Contract_AniDB_AnimeDetailed>(ContractBlob,
                        ContractSize);
                return _contract;
            }
            set
            {
                _contract = value;
                int outsize;
                ContractBlob = CompressionHelper.SerializeObject(value, out outsize);
                ContractSize = outsize;
                ContractVersion = CONTRACT_VERSION;
            }
        }

        public void CollectContractMemory()
        {
            _contract = null;
        }

        private static Logger logger = LogManager.GetCurrentClassLogger();

        // these files come from AniDB but we don't directly save them
        private string reviewIDListRAW;

        public enAnimeType AnimeTypeEnum
        {
            get
            {
                if (AnimeType > 5) return enAnimeType.Other;
                return (enAnimeType) AnimeType;
            }
        }

        public bool FinishedAiring
        {
            get
            {
                if (!EndDate.HasValue) return false; // ongoing

                // all series have finished airing 
                if (EndDate.Value < DateTime.Now) return true;

                return false;
            }
        }

        public string AnimeTypeDescription
        {
            get
            {
                switch (AnimeTypeEnum)
                {
                    case enAnimeType.Movie:
                        return Resources.AnimeType_Movie;
                    case enAnimeType.Other:
                        return Resources.AnimeType_Other;
                    case enAnimeType.OVA:
                        return Resources.AnimeType_OVA;
                    case enAnimeType.TVSeries:
                        return Resources.AnimeType_TVSeries;
                    case enAnimeType.TVSpecial:
                        return Resources.AnimeType_TVSpecial;
                    case enAnimeType.Web:
                        return Resources.AnimeType_Web;
                    default:
                        return Resources.AnimeType_Other;
                }
            }
        }

        public bool IsTvDBLinkDisabled
        {
            get { return (DisableExternalLinksFlag & Constants.FlagLinkTvDB) > 0; }
        }

        public bool IsTraktLinkDisabled
        {
            get { return (DisableExternalLinksFlag & Constants.FlagLinkTrakt) > 0; }
        }

        public bool IsMALLinkDisabled
        {
            get { return (DisableExternalLinksFlag & Constants.FlagLinkMAL) > 0; }
        }

        public bool IsMovieDBLinkDisabled
        {
            get { return (DisableExternalLinksFlag & Constants.FlagLinkMovieDB) > 0; }
        }

        [XmlIgnore]
        public int AirDateAsSeconds
        {
            get { return Utils.GetAniDBDateAsSeconds(AirDate); }
        }

        [XmlIgnore]
        public string AirDateFormatted
        {
            get { return Utils.GetAniDBDate(AirDateAsSeconds); }
        }

        public const int LastYear = 2050;

        [XmlIgnore]
        public string PosterPath
        {
            get
            {
                if (String.IsNullOrEmpty(Picname)) return "";

                return Path.Combine(ImageUtils.GetAniDBImagePath(AnimeID), Picname);
            }
        }

        [XmlIgnore]
        public string Year
        {
            get
            {
                string y = BeginYear.ToString();
                if (BeginYear != EndYear)
                {
                    if (EndYear == LastYear)
                        y += "-Ongoing";
                    else
                        y += "-" + EndYear.ToString();
                }
                return y;
            }
        }

        public List<TvDB_Episode> GetTvDBEpisodes()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetTvDBEpisodes(session.Wrap());
            }
        }

        public List<TvDB_Episode> GetTvDBEpisodes(ISessionWrapper session)
        {
            List<TvDB_Episode> tvDBEpisodes = new List<TvDB_Episode>();

            List<CrossRef_AniDB_TvDBV2> xrefs = GetCrossRefTvDBV2(session);
            if (xrefs.Count == 0) return tvDBEpisodes;


            foreach (CrossRef_AniDB_TvDBV2 xref in xrefs)
            {
                tvDBEpisodes.AddRange(RepoFactory.TvDB_Episode.GetBySeriesID(session, xref.TvDBID));
            }
            return tvDBEpisodes.OrderBy(a=>a.SeasonNumber).ThenBy(a=>a.EpisodeNumber).ToList();
        }

        private Dictionary<int, TvDB_Episode> dictTvDBEpisodes = null;

        public Dictionary<int, TvDB_Episode> GetDictTvDBEpisodes()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetDictTvDBEpisodes(session.Wrap());
            }
        }

        public Dictionary<int, TvDB_Episode> GetDictTvDBEpisodes(ISessionWrapper session)
        {
            if (dictTvDBEpisodes == null)
            {
                try
                {
                    List<TvDB_Episode> tvdbEpisodes = GetTvDBEpisodes(session);
                    if (tvdbEpisodes != null)
                    {
                        dictTvDBEpisodes = new Dictionary<int, TvDB_Episode>();
                        // create a dictionary of absolute episode numbers for tvdb episodes
                        // sort by season and episode number
                        // ignore season 0, which is used for specials
                        List<TvDB_Episode> eps = tvdbEpisodes;

                        int i = 1;
                        foreach (TvDB_Episode ep in eps)
                        {
                            dictTvDBEpisodes[i] = ep;
                            i++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error( ex,ex.ToString());
                }
            }
            return dictTvDBEpisodes;
        }

        private Dictionary<int, int> dictTvDBSeasons = null;

        public Dictionary<int, int> GetDictTvDBSeasons()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetDictTvDBSeasons(session.Wrap());
            }
        }

        public Dictionary<int, int> GetDictTvDBSeasons(ISessionWrapper session)
        {
            if (dictTvDBSeasons == null)
            {
                try
                {
                    List<TvDB_Episode> tvdbEpisodes = GetTvDBEpisodes(session);
                    if (tvdbEpisodes != null)
                    {
                        dictTvDBSeasons = new Dictionary<int, int>();
                        // create a dictionary of season numbers and the first episode for that season

                        List<TvDB_Episode> eps = tvdbEpisodes;
                        int i = 1;
                        int lastSeason = -999;
                        foreach (TvDB_Episode ep in eps)
                        {
                            if (ep.SeasonNumber != lastSeason)
                                dictTvDBSeasons[ep.SeasonNumber] = i;

                            lastSeason = ep.SeasonNumber;
                            i++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error( ex,ex.ToString());
                }
            }
            return dictTvDBSeasons;
        }

        private Dictionary<int, int> dictTvDBSeasonsSpecials = null;

        public Dictionary<int, int> GetDictTvDBSeasonsSpecials()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetDictTvDBSeasonsSpecials(session.Wrap());
            }
        }

        public Dictionary<int, int> GetDictTvDBSeasonsSpecials(ISessionWrapper session)
        {
            if (dictTvDBSeasonsSpecials == null)
            {
                try
                {
                    List<TvDB_Episode> tvdbEpisodes = GetTvDBEpisodes(session);
                    if (tvdbEpisodes != null)
                    {
                        dictTvDBSeasonsSpecials = new Dictionary<int, int>();
                        // create a dictionary of season numbers and the first episode for that season

                        List<TvDB_Episode> eps = tvdbEpisodes;
                        int i = 1;
                        int lastSeason = -999;
                        foreach (TvDB_Episode ep in eps)
                        {
                            if (ep.SeasonNumber > 0) continue;

                            int thisSeason = 0;
                            if (ep.AirsBeforeSeason.HasValue) thisSeason = ep.AirsBeforeSeason.Value;
                            if (ep.AirsAfterSeason.HasValue) thisSeason = ep.AirsAfterSeason.Value;

                            if (thisSeason != lastSeason)
                                dictTvDBSeasonsSpecials[thisSeason] = i;

                            lastSeason = thisSeason;
                            i++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error( ex,ex.ToString());
                }
            }
            return dictTvDBSeasonsSpecials;
        }

        public List<CrossRef_AniDB_TvDB_Episode> GetCrossRefTvDBEpisodes()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetCrossRefTvDBEpisodes(session);
            }
        }

        public List<CrossRef_AniDB_TvDB_Episode> GetCrossRefTvDBEpisodes(ISession session)
        {
            return RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAnimeID(session, AnimeID);
        }

        public List<CrossRef_AniDB_TvDBV2> GetCrossRefTvDBV2()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetCrossRefTvDBV2(session.Wrap());
            }
        }

        public List<CrossRef_AniDB_TvDBV2> GetCrossRefTvDBV2(ISessionWrapper session)
        {
            return RepoFactory.CrossRef_AniDB_TvDBV2.GetByAnimeID(session, this.AnimeID);
        }

        public List<CrossRef_AniDB_TraktV2> GetCrossRefTraktV2()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetCrossRefTraktV2(session);
            }
        }

        public List<CrossRef_AniDB_TraktV2> GetCrossRefTraktV2(ISession session)
        {
            return RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(session, this.AnimeID);
        }

        public List<CrossRef_AniDB_MAL> GetCrossRefMAL()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetCrossRefMAL(session);
            }
        }

        public List<CrossRef_AniDB_MAL> GetCrossRefMAL(ISession session)
        {
            return RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID(session, this.AnimeID);
        }

        public List<TvDB_Series> GetTvDBSeries()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetTvDBSeries(session.Wrap());
            }
        }

        public List<TvDB_Series> GetTvDBSeries(ISessionWrapper session)
        {
            List<TvDB_Series> ret = new List<TvDB_Series>();
            List<CrossRef_AniDB_TvDBV2> xrefs = GetCrossRefTvDBV2(session);
            if (xrefs.Count == 0) return ret;

            foreach (CrossRef_AniDB_TvDBV2 xref in xrefs)
            {
                TvDB_Series ser = RepoFactory.TvDB_Series.GetByTvDBID(session, xref.TvDBID);
                if (ser != null) ret.Add(ser);
            }

            return ret;
        }

        public List<TvDB_ImageFanart> GetTvDBImageFanarts()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetTvDBImageFanarts(session.Wrap());
            }
        }

        public List<TvDB_ImageFanart> GetTvDBImageFanarts(ISessionWrapper session)
        {
            List<TvDB_ImageFanart> ret = new List<TvDB_ImageFanart>();

            List<CrossRef_AniDB_TvDBV2> xrefs = GetCrossRefTvDBV2(session);
            if (xrefs.Count == 0) return ret;

            foreach (CrossRef_AniDB_TvDBV2 xref in xrefs)
            {
                ret.AddRange(RepoFactory.TvDB_ImageFanart.GetBySeriesID(session, xref.TvDBID));
            }


            return ret;
        }

        public List<TvDB_ImagePoster> GetTvDBImagePosters()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetTvDBImagePosters(session.Wrap());
            }
        }

        public List<TvDB_ImagePoster> GetTvDBImagePosters(ISessionWrapper session)
        {
            List<TvDB_ImagePoster> ret = new List<TvDB_ImagePoster>();

            List<CrossRef_AniDB_TvDBV2> xrefs = GetCrossRefTvDBV2(session);
            if (xrefs.Count == 0) return ret;

            foreach (CrossRef_AniDB_TvDBV2 xref in xrefs)
            {
                ret.AddRange(RepoFactory.TvDB_ImagePoster.GetBySeriesID(session, xref.TvDBID));
            }

            return ret;
        }

        public List<TvDB_ImageWideBanner> GetTvDBImageWideBanners()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetTvDBImageWideBanners(session.Wrap());
            }
        }

        public List<TvDB_ImageWideBanner> GetTvDBImageWideBanners(ISessionWrapper session)
        {
            List<TvDB_ImageWideBanner> ret = new List<TvDB_ImageWideBanner>();

            List<CrossRef_AniDB_TvDBV2> xrefs = GetCrossRefTvDBV2(session);
            if (xrefs.Count == 0) return ret;

            foreach (CrossRef_AniDB_TvDBV2 xref in xrefs)
            {
                ret.AddRange(RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(xref.TvDBID));
            }
            return ret;
        }

        public CrossRef_AniDB_Other GetCrossRefMovieDB()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetCrossRefMovieDB(session.Wrap());
            }
        }

        public CrossRef_AniDB_Other GetCrossRefMovieDB(ISessionWrapper criteriaFactory)
        {
            return RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(criteriaFactory, this.AnimeID, CrossRefType.MovieDB);
        }


        public MovieDB_Movie GetMovieDBMovie()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetMovieDBMovie(session.Wrap());
            }
        }

        public MovieDB_Movie GetMovieDBMovie(ISessionWrapper criteriaFactory)
        {
            CrossRef_AniDB_Other xref = GetCrossRefMovieDB(criteriaFactory);
            if (xref == null) return null;
            return RepoFactory.MovieDb_Movie.GetByOnlineID(criteriaFactory, Int32.Parse(xref.CrossRefID));
        }

        public List<MovieDB_Fanart> GetMovieDBFanarts()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetMovieDBFanarts(session.Wrap());
            }
        }

        public List<MovieDB_Fanart> GetMovieDBFanarts(ISessionWrapper session)
        {
            CrossRef_AniDB_Other xref = GetCrossRefMovieDB(session);
            if (xref == null) return new List<MovieDB_Fanart>();

            return RepoFactory.MovieDB_Fanart.GetByMovieID(session, Int32.Parse(xref.CrossRefID));
        }

        public List<MovieDB_Poster> GetMovieDBPosters()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetMovieDBPosters(session.Wrap());
            }
        }

        public List<MovieDB_Poster> GetMovieDBPosters(ISessionWrapper session)
        {
            CrossRef_AniDB_Other xref = GetCrossRefMovieDB(session);
            if (xref == null) return new List<MovieDB_Poster>();

            return RepoFactory.MovieDB_Poster.GetByMovieID(session, Int32.Parse(xref.CrossRefID));
        }

        public AniDB_Anime_DefaultImage GetDefaultPoster()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetDefaultPoster(session.Wrap());
            }
        }

        public AniDB_Anime_DefaultImage GetDefaultPoster(ISessionWrapper criteriaFactory)
        {
            return RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(criteriaFactory, this.AnimeID, (int) ImageSizeType.Poster);
        }

        public string PosterPathNoDefault
        {
            get
            {
                string fileName = Path.Combine(ImageUtils.GetAniDBImagePath(AnimeID), Picname);
                return fileName;
            }
        }



        public string GetDefaultPosterPathNoBlanks()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetDefaultPosterPathNoBlanks(session.Wrap());
            }
        }

        public string GetDefaultPosterPathNoBlanks(ISessionWrapper session)
        {
            AniDB_Anime_DefaultImage defaultPoster = GetDefaultPoster(session);
            if (defaultPoster == null)
                return PosterPathNoDefault;
            else
            {
                ImageEntityType imageType = (ImageEntityType) defaultPoster.ImageParentType;

                switch (imageType)
                {
                    case ImageEntityType.AniDB_Cover:
                        return this.PosterPath;

                    case ImageEntityType.TvDB_Cover:

                        TvDB_ImagePoster tvPoster = RepoFactory.TvDB_ImagePoster.GetByID(session, defaultPoster.ImageParentID);
                        if (tvPoster != null)
                            return tvPoster.FullImagePath;
                        else
                            return this.PosterPath;

                    case ImageEntityType.Trakt_Poster:

                        Trakt_ImagePoster traktPoster = RepoFactory.Trakt_ImagePoster.GetByID(session, defaultPoster.ImageParentID);
                        if (traktPoster != null)
                            return traktPoster.FullImagePath;
                        else
                            return this.PosterPath;

                    case ImageEntityType.MovieDB_Poster:

                        MovieDB_Poster moviePoster = RepoFactory.MovieDB_Poster.GetByID(session, defaultPoster.ImageParentID);
                        if (moviePoster != null)
                            return moviePoster.FullImagePath;
                        else
                            return this.PosterPath;
                }
            }

            return PosterPath;
        }

        public ImageDetails GetDefaultPosterDetailsNoBlanks()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetDefaultPosterDetailsNoBlanks(session.Wrap());
            }
        }

        public ImageDetails GetDefaultPosterDetailsNoBlanks(ISessionWrapper session)
        {
            ImageDetails details = new ImageDetails() {ImageType = JMMImageType.AniDB_Cover, ImageID = this.AnimeID};
            AniDB_Anime_DefaultImage defaultPoster = GetDefaultPoster(session);

            if (defaultPoster == null)
                return details;
            else
            {
                ImageEntityType imageType = (ImageEntityType) defaultPoster.ImageParentType;

                switch (imageType)
                {
                    case ImageEntityType.AniDB_Cover:
                        return details;

                    case ImageEntityType.TvDB_Cover:

                        TvDB_ImagePoster tvPoster = RepoFactory.TvDB_ImagePoster.GetByID(session, defaultPoster.ImageParentID);
                        if (tvPoster != null)
                            details = new ImageDetails()
                            {
                                ImageType = JMMImageType.TvDB_Cover,
                                ImageID = tvPoster.TvDB_ImagePosterID
                            };

                        return details;

                    case ImageEntityType.Trakt_Poster:

                        Trakt_ImagePoster traktPoster = RepoFactory.Trakt_ImagePoster.GetByID(session, defaultPoster.ImageParentID);
                        if (traktPoster != null)
                            details = new ImageDetails()
                            {
                                ImageType = JMMImageType.Trakt_Poster,
                                ImageID = traktPoster.Trakt_ImagePosterID
                            };

                        return details;

                    case ImageEntityType.MovieDB_Poster:

                        MovieDB_Poster moviePoster = RepoFactory.MovieDB_Poster.GetByID(session, defaultPoster.ImageParentID);
                        if (moviePoster != null)
                            details = new ImageDetails()
                            {
                                ImageType = JMMImageType.MovieDB_Poster,
                                ImageID = moviePoster.MovieDB_PosterID
                            };

                        return details;
                }
            }

            return details;
        }

        public AniDB_Anime_DefaultImage GetDefaultFanart()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetDefaultFanart(session.Wrap());
            }
        }

        public AniDB_Anime_DefaultImage GetDefaultFanart(ISessionWrapper factory)
        {
            return RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(factory, this.AnimeID, (int) ImageSizeType.Fanart);
        }

        public ImageDetails GetDefaultFanartDetailsNoBlanks()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetDefaultFanartDetailsNoBlanks(session.Wrap());
            }
        }

        public ImageDetails GetDefaultFanartDetailsNoBlanks(ISessionWrapper session)
        {
            Random fanartRandom = new Random();

            ImageDetails details = null;
            if (GetDefaultFanart(session) == null)
            {
	            List<Contract_AniDB_Anime_DefaultImage> fanarts = Contract.AniDBAnime.Fanarts;
	            if (fanarts == null || fanarts.Count == 0) return null;
	            Contract_AniDB_Anime_DefaultImage art = fanarts[fanartRandom.Next(0, fanarts.Count)];
	            details = new ImageDetails()
	            {
					ImageID = art.AniDB_Anime_DefaultImageID,
		            ImageType = (JMMImageType)art.ImageType
	            };
	            return details;
            }
            else
            {
	            // TODO Move this to contract as well
	            AniDB_Anime_DefaultImage fanart = GetDefaultFanart();
	            ImageEntityType imageType = (ImageEntityType) fanart.ImageParentType;

                switch (imageType)
                {
                    case ImageEntityType.TvDB_FanArt:

                        TvDB_ImageFanart tvFanart = RepoFactory.TvDB_ImageFanart.GetByID(session, fanart.ImageParentID);
                        if (tvFanart != null)
                            details = new ImageDetails()
                            {
                                ImageType = JMMImageType.TvDB_FanArt,
                                ImageID = tvFanart.TvDB_ImageFanartID
                            };

                        return details;

                    case ImageEntityType.Trakt_Fanart:

                        Trakt_ImageFanart traktFanart = RepoFactory.Trakt_ImageFanart.GetByID(session, fanart.ImageParentID);
                        if (traktFanart != null)
                            details = new ImageDetails()
                            {
                                ImageType = JMMImageType.Trakt_Fanart,
                                ImageID = traktFanart.Trakt_ImageFanartID
                            };

                        return details;

                    case ImageEntityType.MovieDB_FanArt:

                        MovieDB_Fanart movieFanart = RepoFactory.MovieDB_Fanart.GetByID(session, fanart.ImageParentID);
                        if (movieFanart != null)
                            details = new ImageDetails()
                            {
                                ImageType = JMMImageType.MovieDB_FanArt,
                                ImageID = movieFanart.MovieDB_FanartID
                            };

                        return details;
                }
            }

            return null;
        }

        public string GetDefaultFanartOnlineURL()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetDefaultFanartOnlineURL(session.Wrap());
            }
        }

        public string GetDefaultFanartOnlineURL(ISessionWrapper session)
        {
            Random fanartRandom = new Random();


            if (GetDefaultFanart() == null)
            {
                // get a random fanart
                if (this.AnimeTypeEnum == enAnimeType.Movie)
                {
                    List<MovieDB_Fanart> fanarts = GetMovieDBFanarts(session);
                    if (fanarts.Count == 0) return "";

                    MovieDB_Fanart movieFanart = fanarts[fanartRandom.Next(0, fanarts.Count)];
                    return movieFanart.URL;
                }
                else
                {
                    List<TvDB_ImageFanart> fanarts = GetTvDBImageFanarts(session);
                    if (fanarts.Count == 0) return null;

                    TvDB_ImageFanart tvFanart = fanarts[fanartRandom.Next(0, fanarts.Count)];
                    return String.Format(Constants.URLS.TvDB_Images, tvFanart.BannerPath);
                }
            }
            else
            {
	            // TODO Move this to contract as well
	            AniDB_Anime_DefaultImage fanart = GetDefaultFanart();
	            ImageEntityType imageType = (ImageEntityType) fanart.ImageParentType;

                switch (imageType)
                {
                    case ImageEntityType.TvDB_FanArt:

                        TvDB_ImageFanart tvFanart = RepoFactory.TvDB_ImageFanart.GetByID(GetDefaultFanart(session).ImageParentID);
                        if (tvFanart != null)
                            return String.Format(Constants.URLS.TvDB_Images, tvFanart.BannerPath);

                        break;

                    case ImageEntityType.Trakt_Fanart:

                        Trakt_ImageFanart traktFanart = RepoFactory.Trakt_ImageFanart.GetByID(GetDefaultFanart(session).ImageParentID);
                        if (traktFanart != null)
                            return traktFanart.ImageURL;

                        break;

                    case ImageEntityType.MovieDB_FanArt:

                        MovieDB_Fanart movieFanart = RepoFactory.MovieDB_Fanart.GetByID(GetDefaultFanart(session).ImageParentID);
                        if (movieFanart != null)
                            return movieFanart.URL;

                        break;
                }
            }

            return "";
        }

        public AniDB_Anime_DefaultImage GetDefaultWideBanner()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetDefaultWideBanner(session.Wrap());
            }
        }

        public AniDB_Anime_DefaultImage GetDefaultWideBanner(ISessionWrapper session)
        {
            return RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(session, this.AnimeID, (int) ImageSizeType.WideBanner);
        }

        public ImageDetails GetDefaultWideBannerDetailsNoBlanks(ISessionWrapper session)
        {
            Random bannerRandom = new Random();

		    ImageDetails details = null;
	        AniDB_Anime_DefaultImage banner = GetDefaultWideBanner();
	        if (banner == null)
		    {
			    // get a random banner (only tvdb)
			    if (this.AnimeTypeEnum == enAnimeType.Movie)
			    {
				    // MovieDB doesn't have banners
				    return null;
			    }
			    else
			    {
				    List<Contract_AniDB_Anime_DefaultImage> banners = Contract.AniDBAnime.Banners;
				    if (banners == null || banners.Count == 0) return null;
				    Contract_AniDB_Anime_DefaultImage art = banners[bannerRandom.Next(0, banners.Count)];
				    details = new ImageDetails()
				    {
					    ImageID = art.AniDB_Anime_DefaultImageID,
					    ImageType = (JMMImageType) art.ImageType
				    };
				    return details;
			    }
		    }
		    else
		    {
			    ImageEntityType imageType = (ImageEntityType) banner.ImageParentType;

                switch (imageType)
                {
                    case ImageEntityType.TvDB_Banner:

						details = new ImageDetails()
						{
							ImageType = JMMImageType.TvDB_Banner,
							ImageID = banner.ToContract(session).TVWideBanner.TvDB_ImageWideBannerID
						};

					    return details;
			    }
		    }

            return null;
        }

        public string AnimeTypeRAW
        {
            get
            {
                return ConvertToRAW((AnimeTypes)AnimeType);
            }
            set { AnimeType = (int) RawToType(value); }
        }

        public static AnimeTypes RawToType(string raw)
        {
            switch (raw.ToLower())
            {
                case "movie":
                    return AnimeTypes.Movie;
                case "ova":
                    return AnimeTypes.OVA;
                case "tv series":
                    return AnimeTypes.TV_Series;
                case "tv special":
                    return AnimeTypes.TV_Special;
                case "web":
                    return AnimeTypes.Web;
                default:
                    return AnimeTypes.Other;
            }
        }
        public static string ConvertToRAW(AnimeTypes t)
        {
            switch (t)
            {
                case AnimeTypes.Movie:
                    return "movie";
                case AnimeTypes.OVA:
                    return "ova";
                case AnimeTypes.TV_Series:
                    return "tv series";
                case AnimeTypes.TV_Special:
                    return "tv special";
                case AnimeTypes.Web:
                    return "web";
                default:
                    return "other";
            }
        }

        [XmlIgnore]
        public string AnimeTypeName
        {
            get { return Enum.GetName(typeof(AnimeTypes), (AnimeTypes) AnimeType).Replace('_', ' '); }
        }

        [XmlIgnore]
        public string TagsString
        {
            get
            {
                List<AniDB_Tag> tags = GetTags();
                string temp = "";
                foreach (AniDB_Tag tag in tags)
                    temp += tag.TagName + "|";
                if (temp.Length > 2)
                    temp = temp.Substring(0, temp.Length - 2);
                return temp;
            }
        }


        [XmlIgnore]
        public bool SearchOnTvDB
        {
            get { return AnimeType != (int) AnimeTypes.Movie; }
        }

        [XmlIgnore]
        public bool SearchOnMovieDB
        {
            get { return AnimeType == (int) AnimeTypes.Movie; }
        }



        public List<AniDB_Tag> GetTags()
        {
            List<AniDB_Tag> tags = new List<AniDB_Tag>();
            foreach (AniDB_Anime_Tag tag in GetAnimeTags())
            {
                AniDB_Tag newTag = RepoFactory.AniDB_Tag.GetByTagID(tag.TagID);
                if (newTag != null) tags.Add(newTag);
            }
            return tags;
        }

        /*public List<AniDB_Anime_Tag> GetAnimeTags()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
                return GetAnimeTags(session);
			}
		}

        public List<AniDB_Anime_Tag> GetAnimeTags(ISession session)
		{
            AniDB_Anime_TagRepository repTagXRef = new AniDB_Anime_TagRepository();
			return repTagXRef.GetByAnimeID(session, AnimeID);
		}

        public List<AniDB_Tag> GetAniDBTags(ISession session)
		{
            AniDB_TagRepository repCats = new AniDB_TagRepository();
			return repCats.GetByAnimeID(session, AnimeID);
		}
        */

        public List<CustomTag> GetCustomTagsForAnime()
        {
            return RepoFactory.CustomTag.GetByAnimeID(AnimeID);
        }

        public List<AniDB_Tag> GetAniDBTags()
        {
            return RepoFactory.AniDB_Tag.GetByAnimeID(AnimeID);
        }

        public List<AniDB_Anime_Tag> GetAnimeTags()
        {
            return RepoFactory.AniDB_Anime_Tag.GetByAnimeID(AnimeID);
        }

        public List<AniDB_Anime_Relation> GetRelatedAnime()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetRelatedAnime(session.Wrap());
            }
        }

        public List<AniDB_Anime_Relation> GetRelatedAnime(ISessionWrapper session)
        {
            return RepoFactory.AniDB_Anime_Relation.GetByAnimeID(session, AnimeID);
        }

        public List<AniDB_Anime_Similar> GetSimilarAnime()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetSimilarAnime(session);
            }
        }

        public List<AniDB_Anime_Similar> GetSimilarAnime(ISession session)
        {
            return RepoFactory.AniDB_Anime_Similar.GetByAnimeID(session, AnimeID);
        }

        [XmlIgnore]
        public List<AniDB_Anime_Review> AnimeReviews
        {
            get
            {
                return RepoFactory.AniDB_Anime_Review.GetByAnimeID(AnimeID);
            }
        }

        public List<AniDB_Anime> GetAllRelatedAnime()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetAllRelatedAnime(session.Wrap());
            }
        }

        public List<AniDB_Anime> GetAllRelatedAnime(ISessionWrapper session)
        {
            List<AniDB_Anime> relList = new List<AniDB_Anime>();
            List<int> relListIDs = new List<int>();
            List<int> searchedIDs = new List<int>();

            GetRelatedAnimeRecursive(session, this.AnimeID, ref relList, ref relListIDs, ref searchedIDs);
            return relList;
        }

        public List<AniDB_Anime_Character> GetAnimeCharacters()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetAnimeCharacters(session.Wrap());
            }
        }

        public List<AniDB_Anime_Character> GetAnimeCharacters(ISessionWrapper session)
        {
            return RepoFactory.AniDB_Anime_Character.GetByAnimeID(session, AnimeID);
        }

        public decimal AniDBRating
        {
            get
            {
                try
                {
                    if (AniDBTotalVotes == 0)
                        return 0;
                    else
                        return AniDBTotalRating/(decimal) AniDBTotalVotes;
                }
                catch (Exception ex)
                {
                    logger.Error("Error in  AniDBRating: {0}", ex.ToString());
                    return 0;
                }
            }
        }

        [XmlIgnore]
        public decimal AniDBTotalRating
        {
            get
            {
                try
                {
                    decimal totalRating = 0;
                    totalRating += (decimal) Rating*VoteCount;
                    totalRating += (decimal) TempRating*TempVoteCount;

                    return totalRating;
                }
                catch (Exception ex)
                {
                    logger.Error("Error in  AniDBRating: {0}", ex.ToString());
                    return 0;
                }
            }
        }

        [XmlIgnore]
        public int AniDBTotalVotes
        {
            get
            {
                try
                {
                    return TempVoteCount + VoteCount;
                }
                catch (Exception ex)
                {
                    logger.Error("Error in  AniDBRating: {0}", ex.ToString());
                    return 0;
                }
            }
        }

        public List<AniDB_Anime_Title> GetTitles()
        {
            return RepoFactory.AniDB_Anime_Title.GetByAnimeID(AnimeID);
        }

        public string GetFormattedTitle(List<AniDB_Anime_Title> titles)
        {
            foreach (NamingLanguage nlan in Languages.PreferredNamingLanguages)
            {
                string thisLanguage = nlan.Language.Trim().ToUpper();

                // Romaji and English titles will be contained in MAIN and/or OFFICIAL
                // we won't use synonyms for these two languages
                if (thisLanguage.Equals(Constants.AniDBLanguageType.Romaji) ||
                    thisLanguage.Equals(Constants.AniDBLanguageType.English))
                {
                    foreach (AniDB_Anime_Title title in titles)
                    {
                        string titleType = title.TitleType.Trim().ToUpper();
                        // first try the  Main title
                        if (titleType == Constants.AnimeTitleType.Main.ToUpper() &&
                            title.Language.Trim().ToUpper() == thisLanguage)
                            return title.Title;
                    }
                }

                // now try the official title
                foreach (AniDB_Anime_Title title in titles)
                {
                    string titleType = title.TitleType.Trim().ToUpper();
                    if (titleType == Constants.AnimeTitleType.Official.ToUpper() &&
                        title.Language.Trim().ToUpper() == thisLanguage)
                        return title.Title;
                }

                // try synonyms
                if (ServerSettings.LanguageUseSynonyms)
                {
                    foreach (AniDB_Anime_Title title in titles)
                    {
                        string titleType = title.TitleType.Trim().ToUpper();
                        if (titleType == Constants.AnimeTitleType.Synonym.ToUpper() &&
                            title.Language.Trim().ToUpper() == thisLanguage)
                            return title.Title;
                    }
                }
            }

            // otherwise just use the main title
            return this.MainTitle;
        }

        public string GetFormattedTitle()
        {
            List<AniDB_Anime_Title> thisTitles = this.GetTitles();
            return GetFormattedTitle(thisTitles);
        }

        [XmlIgnore]
        public AniDB_Vote UserVote
        {
            get
            {
                try
                {
                    return RepoFactory.AniDB_Vote.GetByAnimeID(this.AnimeID);
                }
                catch (Exception ex)
                {
                    logger.Error("Error in  UserVote: {0}", ex.ToString());
                    return null;
                }
            }
        }

        public AniDB_Vote GetUserVote(ISession session)
        {
            try
            {
                return RepoFactory.AniDB_Vote.GetByAnimeID(session, this.AnimeID);
            }
            catch (Exception ex)
            {
                logger.Error("Error in  UserVote: {0}", ex.ToString());
                return null;
            }
        }


        public string PreferredTitle
        {
            get
            {
                List<AniDB_Anime_Title> titles = this.GetTitles();

                foreach (NamingLanguage nlan in Languages.PreferredNamingLanguages)
                {
                    string thisLanguage = nlan.Language.Trim().ToUpper();
                    // Romaji and English titles will be contained in MAIN and/or OFFICIAL
                    // we won't use synonyms for these two languages
                    if (thisLanguage == "X-JAT" || thisLanguage == "EN")
                    {
                        // first try the  Main title
                        for (int i = 0; i < titles.Count; i++)
                        {
                            if (titles[i].Language.Trim().ToUpper() == thisLanguage &&
                                titles[i].TitleType.Trim().ToUpper() == Constants.AnimeTitleType.Main.ToUpper())
                                return titles[i].Title;
                        }
                    }

                    // now try the official title
                    for (int i = 0; i < titles.Count; i++)
                    {
                        if (titles[i].Language.Trim().ToUpper() == thisLanguage &&
                            titles[i].TitleType.Trim().ToUpper() == Constants.AnimeTitleType.Official.ToUpper())
                            return titles[i].Title;
                    }

                    // try synonyms
                    if (ServerSettings.LanguageUseSynonyms)
                    {
                        for (int i = 0; i < titles.Count; i++)
                        {
                            if (titles[i].Language.Trim().ToUpper() == thisLanguage &&
                                titles[i].TitleType.Trim().ToUpper() == Constants.AnimeTitleType.Synonym.ToUpper())
                                return titles[i].Title;
                        }
                    }
                }

                // otherwise just use the main title
                for (int i = 0; i < titles.Count; i++)
                {
                    if (titles[i].TitleType.Trim().ToUpper() == Constants.AnimeTitleType.Main.ToUpper())
                        return titles[i].Title;
                }

                return "ERROR";
            }
        }


        [XmlIgnore]
        public List<AniDB_Episode> AniDBEpisodes
        {
            get
            {
                return RepoFactory.AniDB_Episode.GetByAnimeID(AnimeID);
            }
        }

        public List<AniDB_Episode> GetAniDBEpisodes()
        {
            return RepoFactory.AniDB_Episode.GetByAnimeID(AnimeID);
        }

        public AniDB_Anime()
        {
            this.DisableExternalLinksFlag = 0;
        }

        private void Populate(Raw_AniDB_Anime animeInfo)
        {
            this.AirDate = animeInfo.AirDate;
            this.AllCinemaID = animeInfo.AllCinemaID;
            this.AnimeID = animeInfo.AnimeID;
            //this.AnimeNfo = animeInfo.AnimeNfoID;
            this.AnimePlanetID = animeInfo.AnimePlanetID;
            this.AnimeTypeRAW = animeInfo.AnimeTypeRAW;
            this.ANNID = animeInfo.ANNID;
            this.AvgReviewRating = animeInfo.AvgReviewRating;
            this.AwardList = animeInfo.AwardList;
            this.BeginYear = animeInfo.BeginYear;
            this.DateTimeDescUpdated = DateTime.Now;
            this.DateTimeUpdated = DateTime.Now;
            this.Description = animeInfo.Description;
            this.EndDate = animeInfo.EndDate;
            this.EndYear = animeInfo.EndYear;
            this.MainTitle = animeInfo.MainTitle;
            this.AllTitles = "";
            this.AllTags = "";
            //this.EnglishName = animeInfo.EnglishName;
            this.EpisodeCount = animeInfo.EpisodeCount;
            this.EpisodeCountNormal = animeInfo.EpisodeCountNormal;
            this.EpisodeCountSpecial = animeInfo.EpisodeCountSpecial;
            //this.genre
            this.ImageEnabled = 1;
            //this.KanjiName = animeInfo.KanjiName;
            this.LatestEpisodeNumber = animeInfo.LatestEpisodeNumber;
            //this.OtherName = animeInfo.OtherName;
            this.Picname = animeInfo.Picname;
            this.Rating = animeInfo.Rating;
            //this.relations
            this.Restricted = animeInfo.Restricted;
            this.ReviewCount = animeInfo.ReviewCount;
            //this.RomajiName = animeInfo.RomajiName;
            //this.ShortNames = animeInfo.ShortNames.Replace("'", "|");
            //this.Synonyms = animeInfo.Synonyms.Replace("'", "|");
            this.TempRating = animeInfo.TempRating;
            this.TempVoteCount = animeInfo.TempVoteCount;
            this.URL = animeInfo.URL;
            this.VoteCount = animeInfo.VoteCount;
        }

        public void PopulateAndSaveFromHTTP(ISession session, Raw_AniDB_Anime animeInfo, List<Raw_AniDB_Episode> eps,
            List<Raw_AniDB_Anime_Title> titles,
            List<Raw_AniDB_Category> cats, List<Raw_AniDB_Tag> tags, List<Raw_AniDB_Character> chars,
            List<Raw_AniDB_RelatedAnime> rels, List<Raw_AniDB_SimilarAnime> sims,
            List<Raw_AniDB_Recommendation> recs, bool downloadRelations)
        {
            logger.Trace("------------------------------------------------");
            logger.Trace(String.Format("PopulateAndSaveFromHTTP: for {0} - {1}", animeInfo.AnimeID, animeInfo.MainTitle));
            logger.Trace("------------------------------------------------");

            Stopwatch taskTimer = new Stopwatch();
            Stopwatch totalTimer = Stopwatch.StartNew();

            Populate(animeInfo);

            // save now for FK purposes
            RepoFactory.AniDB_Anime.Save(this);

            taskTimer.Start();

            CreateEpisodes(eps);
            taskTimer.Stop();
            logger.Trace("CreateEpisodes in : " + taskTimer.ElapsedMilliseconds);
            taskTimer.Restart();

            CreateTitles(titles);
            taskTimer.Stop();
            logger.Trace("CreateTitles in : " + taskTimer.ElapsedMilliseconds);
            taskTimer.Restart();

            CreateTags(tags);
            taskTimer.Stop();
            logger.Trace("CreateTags in : " + taskTimer.ElapsedMilliseconds);
            taskTimer.Restart();

            CreateCharacters(session, chars);
            taskTimer.Stop();
            logger.Trace("CreateCharacters in : " + taskTimer.ElapsedMilliseconds);
            taskTimer.Restart();

            CreateRelations(session, rels, downloadRelations);
            taskTimer.Stop();
            logger.Trace("CreateRelations in : " + taskTimer.ElapsedMilliseconds);
            taskTimer.Restart();

            CreateSimilarAnime(session, sims);
            taskTimer.Stop();
            logger.Trace("CreateSimilarAnime in : " + taskTimer.ElapsedMilliseconds);
            taskTimer.Restart();

            CreateRecommendations(session, recs);
            taskTimer.Stop();
            logger.Trace("CreateRecommendations in : " + taskTimer.ElapsedMilliseconds);
            taskTimer.Restart();

            RepoFactory.AniDB_Anime.Save(this);
            totalTimer.Stop();
            logger.Trace("TOTAL TIME in : " + totalTimer.ElapsedMilliseconds);
            logger.Trace("------------------------------------------------");
        }

        /// <summary>
        /// we are depending on the HTTP api call to get most of the info
        /// we only use UDP to get mssing information
        /// </summary>
        /// <param name="animeInfo"></param>
        public void PopulateAndSaveFromUDP(Raw_AniDB_Anime animeInfo)
        {
            // raw fields
            this.reviewIDListRAW = animeInfo.ReviewIDListRAW;

            // save now for FK purposes
            RepoFactory.AniDB_Anime.Save(this);

            CreateAnimeReviews();
        }

        private void CreateEpisodes(List<Raw_AniDB_Episode> eps)
        {
            if (eps == null) return;

            
            this.EpisodeCountSpecial = 0;
            this.EpisodeCountNormal = 0;

            List<AnimeEpisode> animeEpsToDelete = new List<AnimeEpisode>();
            List<AniDB_Episode> aniDBEpsToDelete = new List<AniDB_Episode>();

            foreach (Raw_AniDB_Episode epraw in eps)
            {
                //
                // we need to do this check because some times AniDB will replace an existing episode with a new episode
                List<AniDB_Episode> existingEps = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeTypeNumber(epraw.AnimeID, (enEpisodeType)epraw.EpisodeType, epraw.EpisodeNumber);
                
                // delete any old records
                foreach (AniDB_Episode epOld in existingEps)
                {
                    if (epOld.EpisodeID != epraw.EpisodeID)
                    {
                        // first delete any AnimeEpisode records that point to the new anidb episode
                        AnimeEpisode aniep = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(epOld.EpisodeID);
                        if (aniep != null)
                            animeEpsToDelete.Add(aniep);
                        aniDBEpsToDelete.Add(epOld);
                    }
                }
            }
            RepoFactory.AnimeEpisode.Delete(animeEpsToDelete);
            RepoFactory.AniDB_Episode.Delete(aniDBEpsToDelete);


            List<AniDB_Episode> epsToSave = new List<AniDB_Episode>();
            foreach (Raw_AniDB_Episode epraw in eps)
            {
                AniDB_Episode epNew = RepoFactory.AniDB_Episode.GetByEpisodeID(epraw.EpisodeID);
                if (epNew == null) epNew = new AniDB_Episode();

                epNew.Populate(epraw);
                epsToSave.Add(epNew);

                // since the HTTP api doesn't return a count of the number of specials, we will calculate it here
                if (epNew.EpisodeTypeEnum == enEpisodeType.Episode)
                    this.EpisodeCountNormal++;

                if (epNew.EpisodeTypeEnum == enEpisodeType.Special)
                    this.EpisodeCountSpecial++;
            }
            RepoFactory.AniDB_Episode.Save(epsToSave);

            this.EpisodeCount = EpisodeCountSpecial + EpisodeCountNormal;
        }

        private void CreateTitles(List<Raw_AniDB_Anime_Title> titles)
        {

            if (titles == null) return;

            this.AllTitles = "";

            List<AniDB_Anime_Title> titlesToDelete = RepoFactory.AniDB_Anime_Title.GetByAnimeID(AnimeID);
            List<AniDB_Anime_Title> titlesToSave = new List<AniDB_Anime_Title>();
            foreach (Raw_AniDB_Anime_Title rawtitle in titles)
            {
                AniDB_Anime_Title title = new AniDB_Anime_Title();
                title.Populate(rawtitle);
                titlesToSave.Add(title);

                if (this.AllTitles.Length > 0) this.AllTitles += "|";
                this.AllTitles += rawtitle.Title;
            }
            RepoFactory.AniDB_Anime_Title.Delete(titlesToDelete);
            RepoFactory.AniDB_Anime_Title.Save(titlesToSave);
        }

        private void CreateTags(List<Raw_AniDB_Tag> tags)
        {
            if (tags == null) return;

            this.AllTags = "";


            List<AniDB_Tag> tagsToSave = new List<AniDB_Tag>();
            List<AniDB_Anime_Tag> xrefsToSave = new List<AniDB_Anime_Tag>();
            List<AniDB_Anime_Tag> xrefsToDelete = new List<AniDB_Anime_Tag>();

            // find all the current links, and then later remove the ones that are no longer relevant
            List<AniDB_Anime_Tag> currentTags = RepoFactory.AniDB_Anime_Tag.GetByAnimeID(AnimeID);
            List<int> newTagIDs = new List<int>();

            foreach (Raw_AniDB_Tag rawtag in tags)
            {
                AniDB_Tag tag = RepoFactory.AniDB_Tag.GetByTagID(rawtag.TagID);
                if (tag == null) tag = new AniDB_Tag();

                tag.Populate(rawtag);
                tagsToSave.Add(tag);

                newTagIDs.Add(tag.TagID);

                AniDB_Anime_Tag anime_tag = RepoFactory.AniDB_Anime_Tag.GetByAnimeIDAndTagID(rawtag.AnimeID, rawtag.TagID);
                if (anime_tag == null) anime_tag = new AniDB_Anime_Tag();

                anime_tag.Populate(rawtag);
                xrefsToSave.Add(anime_tag);

                if (this.AllTags.Length > 0) this.AllTags += "|";
                this.AllTags += tag.TagName;
            }

            foreach (AniDB_Anime_Tag curTag in currentTags)
            {
                if (!newTagIDs.Contains(curTag.TagID))
                    xrefsToDelete.Add(curTag);
            }
            RepoFactory.AniDB_Tag.Save(tagsToSave);
            RepoFactory.AniDB_Anime_Tag.Save(xrefsToSave);
            RepoFactory.AniDB_Anime_Tag.Delete(xrefsToDelete);
        }

        private void CreateCharacters(ISession session, List<Raw_AniDB_Character> chars)
        {
            if (chars == null) return;


            ISessionWrapper sessionWrapper = session.Wrap();

            // delete all the existing cross references just in case one has been removed
            List<AniDB_Anime_Character> animeChars = RepoFactory.AniDB_Anime_Character.GetByAnimeID(sessionWrapper, AnimeID);

            RepoFactory.AniDB_Anime_Character.Delete(animeChars);


            List<AniDB_Character> chrsToSave = new List<AniDB_Character>();
            List<AniDB_Anime_Character> xrefsToSave = new List<AniDB_Anime_Character>();

            Dictionary<int, AniDB_Seiyuu> seiyuuToSave = new Dictionary<int, AniDB_Seiyuu>();
            List<AniDB_Character_Seiyuu> seiyuuXrefToSave = new List<AniDB_Character_Seiyuu>();

            // delete existing relationships to seiyuu's
            List<AniDB_Character_Seiyuu> charSeiyuusToDelete = new List<AniDB_Character_Seiyuu>();
            foreach (Raw_AniDB_Character rawchar in chars)
            {
                // delete existing relationships to seiyuu's
                List<AniDB_Character_Seiyuu> allCharSei = RepoFactory.AniDB_Character_Seiyuu.GetByCharID(session, rawchar.CharID);
                foreach (AniDB_Character_Seiyuu xref in allCharSei)
                    charSeiyuusToDelete.Add(xref);
            }
            RepoFactory.AniDB_Character_Seiyuu.Delete(charSeiyuusToDelete);

            foreach (Raw_AniDB_Character rawchar in chars)
            {
                AniDB_Character chr = RepoFactory.AniDB_Character.GetByCharID(sessionWrapper, rawchar.CharID);
                if (chr == null)
                    chr = new AniDB_Character();

                chr.PopulateFromHTTP(rawchar);
                chrsToSave.Add(chr);

                // create cross ref's between anime and character, but don't actually download anything
                AniDB_Anime_Character anime_char = new AniDB_Anime_Character();
                anime_char.Populate(rawchar);
                xrefsToSave.Add(anime_char);

                foreach (Raw_AniDB_Seiyuu rawSeiyuu in rawchar.Seiyuus)
                {
                    // save the link between character and seiyuu
                    AniDB_Character_Seiyuu acc = RepoFactory.AniDB_Character_Seiyuu.GetByCharIDAndSeiyuuID(session, rawchar.CharID,
                        rawSeiyuu.SeiyuuID);
                    if (acc == null)
                    {
                        acc = new AniDB_Character_Seiyuu();
                        acc.CharID = chr.CharID;
                        acc.SeiyuuID = rawSeiyuu.SeiyuuID;
                        seiyuuXrefToSave.Add(acc);
                    }

                    // save the seiyuu
                    AniDB_Seiyuu seiyuu = RepoFactory.AniDB_Seiyuu.GetBySeiyuuID(session, rawSeiyuu.SeiyuuID);
                    if (seiyuu == null) seiyuu = new AniDB_Seiyuu();
                    seiyuu.PicName = rawSeiyuu.PicName;
                    seiyuu.SeiyuuID = rawSeiyuu.SeiyuuID;
                    seiyuu.SeiyuuName = rawSeiyuu.SeiyuuName;
                    seiyuuToSave[seiyuu.SeiyuuID] = seiyuu;
                }
            }
            RepoFactory.AniDB_Character.Save(chrsToSave);
            RepoFactory.AniDB_Anime_Character.Save(xrefsToSave);
            RepoFactory.AniDB_Seiyuu.Save(seiyuuToSave.Values.ToList());
            RepoFactory.AniDB_Character_Seiyuu.Save(seiyuuXrefToSave);

        }

        private void CreateRelations(ISession session, List<Raw_AniDB_RelatedAnime> rels, bool downloadRelations)
        {
            if (rels == null) return;


            List<AniDB_Anime_Relation> relsToSave = new List<AniDB_Anime_Relation>();
            List<CommandRequest_GetAnimeHTTP> cmdsToSave = new List<CommandRequest_GetAnimeHTTP>();

            foreach (Raw_AniDB_RelatedAnime rawrel in rels)
            {
                AniDB_Anime_Relation anime_rel = RepoFactory.AniDB_Anime_Relation.GetByAnimeIDAndRelationID(session, rawrel.AnimeID,
                    rawrel.RelatedAnimeID);
                if (anime_rel == null) anime_rel = new AniDB_Anime_Relation();

                anime_rel.Populate(rawrel);
                relsToSave.Add(anime_rel);

                if (downloadRelations && ServerSettings.AutoGroupSeries)
                {
                    logger.Info("Adding command to download related anime for {0} ({1}), related anime ID = {2}",
                        this.MainTitle, this.AnimeID, anime_rel.RelatedAnimeID);

                    // I have disable the downloading of relations here because of banning issues
                    // basically we will download immediate relations, but not relations of relations

                    //CommandRequest_GetAnimeHTTP cr_anime = new CommandRequest_GetAnimeHTTP(rawrel.RelatedAnimeID, false, downloadRelations);
                    CommandRequest_GetAnimeHTTP cr_anime = new CommandRequest_GetAnimeHTTP(anime_rel.RelatedAnimeID,
                        false, false);
                    cmdsToSave.Add(cr_anime);
                }
            }
            RepoFactory.AniDB_Anime_Relation.Save(relsToSave);

            // this is not part of the session/transaction because it does other operations in the save
            foreach (CommandRequest_GetAnimeHTTP cmd in cmdsToSave)
                cmd.Save();
        }

        private void CreateSimilarAnime(ISession session, List<Raw_AniDB_SimilarAnime> sims)
        {
            if (sims == null) return;


            List<AniDB_Anime_Similar> recsToSave = new List<AniDB_Anime_Similar>();

            foreach (Raw_AniDB_SimilarAnime rawsim in sims)
            {
                AniDB_Anime_Similar anime_sim = RepoFactory.AniDB_Anime_Similar.GetByAnimeIDAndSimilarID(session, rawsim.AnimeID,
                    rawsim.SimilarAnimeID);
                if (anime_sim == null) anime_sim = new AniDB_Anime_Similar();

                anime_sim.Populate(rawsim);
                recsToSave.Add(anime_sim);
            }
            RepoFactory.AniDB_Anime_Similar.Save(recsToSave);
        }

        private void CreateRecommendations(ISession session, List<Raw_AniDB_Recommendation> recs)
        {
            if (recs == null) return;

            //AniDB_RecommendationRepository repRecs = new AniDB_RecommendationRepository();

            List<AniDB_Recommendation> recsToSave = new List<AniDB_Recommendation>();
            foreach (Raw_AniDB_Recommendation rawRec in recs)
            {
                AniDB_Recommendation rec = RepoFactory.AniDB_Recommendation.GetByAnimeIDAndUserID(session, rawRec.AnimeID, rawRec.UserID);                
                if (rec == null)
                    rec = new AniDB_Recommendation();
                rec.Populate(rawRec);
                recsToSave.Add(rec);
            }
            RepoFactory.AniDB_Recommendation.Save(recsToSave);
        }

        private void CreateAnimeReviews()
        {
            if (reviewIDListRAW != null)
                //Only create relations if the origin of the data if from Raw (WebService/AniDB)
            {
                if (reviewIDListRAW.Trim().Length == 0)
                    return;

                //Delete old if changed
                List<AniDB_Anime_Review> animeReviews = RepoFactory.AniDB_Anime_Review.GetByAnimeID(AnimeID);
                foreach (AniDB_Anime_Review xref in animeReviews)
                {
                    RepoFactory.AniDB_Anime_Review.Delete(xref.AniDB_Anime_ReviewID);
                }


                string[] revs = reviewIDListRAW.Split(',');
                foreach (string review in revs)
                {
                    if (review.Trim().Length > 0)
                    {
                        int rev = 0;
                        Int32.TryParse(review.Trim(), out rev);
                        if (rev != 0)
                        {
                            AniDB_Anime_Review csr = new AniDB_Anime_Review();
                            csr.AnimeID = this.AnimeID;
                            csr.ReviewID = rev;
                            RepoFactory.AniDB_Anime_Review.Save(csr);
                        }
                    }
                }
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("AnimeID: " + AnimeID);
            sb.Append(" | Main Title: " + MainTitle);
            sb.Append(" | EpisodeCount: " + EpisodeCount);
            sb.Append(" | AirDate: " + AirDate);
            sb.Append(" | Picname: " + Picname);
            sb.Append(" | Type: " + AnimeTypeRAW);
            return sb.ToString();
        }

        private Contract_AniDBAnime GenerateContract(ISessionWrapper session, List<AniDB_Anime_Title> titles)
        {
            List<Contract_AniDB_Character> characters = GetCharactersContract();
            List<MovieDB_Fanart> movDbFanart = null;
            List<TvDB_ImageFanart> tvDbFanart = null;
            List<TvDB_ImageWideBanner> tvDbBanners = null;

            if (AnimeTypeEnum == enAnimeType.Movie)
            {
                movDbFanart = GetMovieDBFanarts(session);
            }
            else
            {
                tvDbFanart = GetTvDBImageFanarts(session);
                tvDbBanners = GetTvDBImageWideBanners(session);
            }

            Contract_AniDBAnime contract = GenerateContract(titles, null, characters, movDbFanart, tvDbFanart, tvDbBanners);
            AniDB_Anime_DefaultImage defFanart = GetDefaultFanart(session);
            AniDB_Anime_DefaultImage defPoster = GetDefaultPoster(session);
            AniDB_Anime_DefaultImage defBanner = GetDefaultWideBanner(session);

            contract.DefaultImageFanart = defFanart?.ToContract(session);
            contract.DefaultImagePoster = defPoster?.ToContract(session);
            contract.DefaultImageWideBanner = defBanner?.ToContract(session);

	        return contract;
        }

        private Contract_AniDBAnime GenerateContract(List<AniDB_Anime_Title> titles, DefaultAnimeImages defaultImages,
            List<Contract_AniDB_Character> characters, IEnumerable<MovieDB_Fanart> movDbFanart, IEnumerable<TvDB_ImageFanart> tvDbFanart,
            IEnumerable<TvDB_ImageWideBanner> tvDbBanners)
        {
            Contract_AniDBAnime contract = new Contract_AniDBAnime();
            contract.AirDate = this.AirDate;
            contract.AllCinemaID = this.AllCinemaID;

            //TODO this can 
            contract.AllTags =
                new HashSet<string>(
                    this.AllTags.Split(new char[] {'|'}, StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.Trim())
                        .Where(a => !string.IsNullOrEmpty(a)), StringComparer.InvariantCultureIgnoreCase);
            contract.AllTitles =
                new HashSet<string>(
                    this.AllTitles.Split(new char[] {'|'}, StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.Trim())
                        .Where(a => !string.IsNullOrEmpty(a)), StringComparer.InvariantCultureIgnoreCase);
            contract.AnimeID = this.AnimeID;
            contract.AnimeNfo = this.AnimeNfo;
            contract.AnimePlanetID = this.AnimePlanetID;
            contract.AnimeType = this.AnimeType;
            contract.ANNID = this.ANNID;
            contract.AvgReviewRating = this.AvgReviewRating;
            contract.AwardList = this.AwardList;
            contract.BeginYear = this.BeginYear;
            contract.Description = this.Description;
            contract.DateTimeDescUpdated = this.DateTimeDescUpdated;
            contract.DateTimeUpdated = this.DateTimeUpdated;
            contract.EndDate = this.EndDate;
            contract.EndYear = this.EndYear;
            contract.EpisodeCount = this.EpisodeCount;
            contract.EpisodeCountNormal = this.EpisodeCountNormal;
            contract.EpisodeCountSpecial = this.EpisodeCountSpecial;
            contract.ImageEnabled = this.ImageEnabled;
            contract.LatestEpisodeNumber = this.LatestEpisodeNumber;
            contract.MainTitle = this.MainTitle;
            contract.Picname = this.Picname;
            contract.Rating = this.Rating;
            contract.Restricted = this.Restricted;
            contract.ReviewCount = this.ReviewCount;
            contract.TempRating = this.TempRating;
            contract.TempVoteCount = this.TempVoteCount;
            contract.URL = this.URL;
            contract.VoteCount = this.VoteCount;
            contract.FormattedTitle = GetFormattedTitle(titles);
            contract.DisableExternalLinksFlag = this.DisableExternalLinksFlag;
            contract.Characters = characters;

            if (defaultImages != null)
            {
                contract.DefaultImageFanart = defaultImages.Fanart?.ToContract();
                contract.DefaultImagePoster = defaultImages.Poster?.ToContract();
                contract.DefaultImageWideBanner = defaultImages.WideBanner?.ToContract();
            }

            if (AnimeTypeEnum == enAnimeType.Movie)
            {
                contract.Fanarts = movDbFanart?.Select(a => new Contract_AniDB_Anime_DefaultImage
                    {
                        ImageType = (int)JMMImageType.MovieDB_FanArt,
                        MovieFanart = a.ToContract(),
                        AniDB_Anime_DefaultImageID = a.MovieDB_FanartID
                    })
                    .ToList();
            }
            else // Not a movie
            {
                contract.Fanarts = tvDbFanart?.Select(a => new Contract_AniDB_Anime_DefaultImage
                    {
                        ImageType = (int)JMMImageType.TvDB_FanArt,
                        TVFanart = a.ToContract(),
                        AniDB_Anime_DefaultImageID = a.TvDB_ImageFanartID
                    })
                    .ToList();
                contract.Banners = tvDbBanners?.Select(a => new Contract_AniDB_Anime_DefaultImage
                    {
                        ImageType = (int)JMMImageType.TvDB_Banner,
                        TVWideBanner = a.ToContract(),
                        AniDB_Anime_DefaultImageID = a.TvDB_ImageWideBannerID
                    })
                    .ToList();
            }

	        if (contract.Fanarts?.Count == 0) contract.Fanarts = null;
	        if (contract.Banners?.Count == 0) contract.Banners = null;

	        return contract;
        }

        public List<Contract_AniDB_Character> GetCharactersContract()
        {
            List<Contract_AniDB_Character> chars = new List<Contract_AniDB_Character>();

            try
            {

                List<AniDB_Anime_Character> animeChars = RepoFactory.AniDB_Anime_Character.GetByAnimeID(this.AnimeID);
                if (animeChars == null || animeChars.Count == 0) return chars;

                foreach (AniDB_Anime_Character animeChar in animeChars)
                {
                    AniDB_Character chr = RepoFactory.AniDB_Character.GetByCharID(animeChar.CharID);
                    if (chr != null)
                        chars.Add(chr.ToContract(animeChar.CharType));
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
            return chars;
        }

        public static void UpdateContractDetailedBatch(ISessionWrapper session, IReadOnlyCollection<AniDB_Anime> animeColl)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (animeColl == null)
                throw new ArgumentNullException(nameof(animeColl));

            int[] animeIds = animeColl.Select(a => a.AnimeID).ToArray();

            var titlesByAnime = RepoFactory.AniDB_Anime_Title.GetByAnimeIDs(session, animeIds);
            var animeTagsByAnime = RepoFactory.AniDB_Anime_Tag.GetByAnimeIDs(session, animeIds);
            var tagsByAnime = RepoFactory.AniDB_Tag.GetByAnimeIDs(session, animeIds);
            var custTagsByAnime = RepoFactory.CustomTag.GetByAnimeIDs(session, animeIds);
            var voteByAnime = RepoFactory.AniDB_Vote.GetByAnimeIDs(session, animeIds);
            var audioLangByAnime = RepoFactory.Adhoc.GetAudioLanguageStatsByAnime(session, animeIds);
            var subtitleLangByAnime = RepoFactory.Adhoc.GetSubtitleLanguageStatsByAnime(session, animeIds);
            var vidQualByAnime = RepoFactory.Adhoc.GetAllVideoQualityByAnime(session, animeIds);
            var epVidQualByAnime = RepoFactory.Adhoc.GetEpisodeVideoQualityStatsByAnime(session, animeIds);
            var defImagesByAnime = RepoFactory.AniDB_Anime.GetDefaultImagesByAnime(session, animeIds);
            var charsByAnime = RepoFactory.AniDB_Character.GetCharacterAndSeiyuuByAnime(session, animeIds);
            var movDbFanartByAnime = RepoFactory.MovieDB_Fanart.GetByAnimeIDs(session, animeIds);
            var tvDbBannersByAnime = RepoFactory.TvDB_ImageWideBanner.GetByAnimeIDs(session, animeIds);
            var tvDbFanartByAnime = RepoFactory.TvDB_ImageFanart.GetByAnimeIDs(session, animeIds);

            foreach (AniDB_Anime anime in animeColl)
            {
                var contract = new Contract_AniDB_AnimeDetailed();
                var animeTitles = titlesByAnime[anime.AnimeID];
                DefaultAnimeImages defImages;

                defImagesByAnime.TryGetValue(anime.AnimeID, out defImages);

                var characterContracts = (charsByAnime[anime.AnimeID] ?? Enumerable.Empty<AnimeCharacterAndSeiyuu>())
                    .Select(ac => ac.ToContract())
                    .ToList();
                var movieDbFanart = movDbFanartByAnime[anime.AnimeID];
                var tvDbBanners = tvDbBannersByAnime[anime.AnimeID];
                var tvDbFanart = tvDbFanartByAnime[anime.AnimeID];

                contract.AniDBAnime = anime.GenerateContract(animeTitles.ToList(), defImages, characterContracts,
                    movieDbFanart, tvDbFanart, tvDbBanners);

                // Anime titles
                contract.AnimeTitles = titlesByAnime[anime.AnimeID]
                    .Select(t => new Contract_AnimeTitle
                        {
                            AnimeID = t.AnimeID,
                            Language = t.Language,
                            Title = t.Title,
                            TitleType = t.TitleType
                        }).ToList();

                // Anime tags
                var dictAnimeTags = animeTagsByAnime[anime.AnimeID]
                    .ToDictionary(t => t.TagID);

                contract.Tags = tagsByAnime[anime.AnimeID].Select(t =>
                    {
                        AniDB_Anime_Tag animeTag = null;
                        Contract_AnimeTag ctag = new Contract_AnimeTag
                            {
                                GlobalSpoiler = t.GlobalSpoiler,
                                LocalSpoiler = t.LocalSpoiler,
                                TagDescription = t.TagDescription,
                                TagID = t.TagID,
                                TagName = t.TagName,
                                Weight = dictAnimeTags.TryGetValue(t.TagID, out animeTag) ? animeTag.Weight : 0
                            };

                        return ctag;
                    }).ToList();

                // Custom tags
                contract.CustomTags = custTagsByAnime[anime.AnimeID]
                    .Select(t => t.ToContract())
                    .ToList();

                // Vote
                AniDB_Vote vote;

                if (voteByAnime.TryGetValue(anime.AnimeID, out vote))
                {
                    contract.UserVote = vote.ToContract();
                }

                LanguageStat langStat;

                // Subtitle languages
                contract.Stat_AudioLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

                if (audioLangByAnime.TryGetValue(anime.AnimeID, out langStat))
                {
                    contract.Stat_AudioLanguages.UnionWith(langStat.LanguageNames);
                }

                // Audio languages
                contract.Stat_SubtitleLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

                if (subtitleLangByAnime.TryGetValue(anime.AnimeID, out langStat))
                {
                    contract.Stat_SubtitleLanguages.UnionWith(langStat.LanguageNames);
                }

                // Anime video quality
                HashSet<string> vidQual;

                contract.Stat_AllVideoQuality = vidQualByAnime.TryGetValue(anime.AnimeID, out vidQual) ? vidQual
                    : new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

                // Episode video quality
                AnimeVideoQualityStat vidQualStat;

                contract.Stat_AllVideoQuality_Episodes = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

                if (epVidQualByAnime.TryGetValue(anime.AnimeID, out vidQualStat) && vidQualStat.VideoQualityEpisodeCount.Count > 0)
                {
                    contract.Stat_AllVideoQuality_Episodes.UnionWith(vidQualStat.VideoQualityEpisodeCount
                        .Where(kvp => kvp.Value >= anime.EpisodeCountNormal)
                        .Select(kvp => kvp.Key));
                }

                anime.Contract = contract;
            }
        }

        public void UpdateContractDetailed(ISessionWrapper session)
        {
            List<AniDB_Anime_Title> animeTitles = RepoFactory.AniDB_Anime_Title.GetByAnimeID(AnimeID);
            Contract_AniDB_AnimeDetailed contract = new Contract_AniDB_AnimeDetailed();
            contract.AniDBAnime = GenerateContract(session, animeTitles);


            contract.AnimeTitles = new List<Contract_AnimeTitle>();
            contract.Tags = new List<Contract_AnimeTag>();
            contract.CustomTags = new List<Contract_CustomTag>();

            // get all the anime titles
            if (animeTitles != null)
            {
                foreach (AniDB_Anime_Title title in animeTitles)
                {
                    Contract_AnimeTitle ctitle = new Contract_AnimeTitle();
                    ctitle.AnimeID = title.AnimeID;
                    ctitle.Language = title.Language;
                    ctitle.Title = title.Title;
                    ctitle.TitleType = title.TitleType;
                    contract.AnimeTitles.Add(ctitle);
                }
            }


            Dictionary<int, AniDB_Anime_Tag> dictAnimeTags = new Dictionary<int, AniDB_Anime_Tag>();
            foreach (AniDB_Anime_Tag animeTag in GetAnimeTags())
                dictAnimeTags[animeTag.TagID] = animeTag;

            foreach (AniDB_Tag tag in GetAniDBTags())
            {
                Contract_AnimeTag ctag = new Contract_AnimeTag();

                ctag.GlobalSpoiler = tag.GlobalSpoiler;
                ctag.LocalSpoiler = tag.LocalSpoiler;
                //ctag.Spoiler = tag.Spoiler;
                //ctag.TagCount = tag.TagCount;
                ctag.TagDescription = tag.TagDescription;
                ctag.TagID = tag.TagID;
                ctag.TagName = tag.TagName;

                if (dictAnimeTags.ContainsKey(tag.TagID))
                    ctag.Weight = dictAnimeTags[tag.TagID].Weight;
                else
                    ctag.Weight = 0;

                contract.Tags.Add(ctag);
            }


            // Get all the custom tags
            foreach (CustomTag custag in GetCustomTagsForAnime())
                contract.CustomTags.Add(custag.ToContract());

            if (this.UserVote != null)
                contract.UserVote = this.UserVote.ToContract();

            HashSet<string> audioLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            HashSet<string> subtitleLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            //logger.Trace(" XXXX 06");

            // audio languages
            Dictionary<int, LanguageStat> dicAudio = RepoFactory.Adhoc.GetAudioLanguageStatsByAnime(session, this.AnimeID);
            foreach (KeyValuePair<int, LanguageStat> kvp in dicAudio)
            {
                foreach (string lanName in kvp.Value.LanguageNames)
                {
                    if (!audioLanguages.Contains(lanName))
                        audioLanguages.Add(lanName);
                }
            }

            //logger.Trace(" XXXX 07");

            // subtitle languages
            Dictionary<int, LanguageStat> dicSubtitle = RepoFactory.Adhoc.GetSubtitleLanguageStatsByAnime(session, this.AnimeID);
            foreach (KeyValuePair<int, LanguageStat> kvp in dicSubtitle)
            {
                foreach (string lanName in kvp.Value.LanguageNames)
                {
                    if (!subtitleLanguages.Contains(lanName))
                        subtitleLanguages.Add(lanName);
                }
            }

            //logger.Trace(" XXXX 08");

            contract.Stat_AudioLanguages = audioLanguages;

            //logger.Trace(" XXXX 09");

            contract.Stat_SubtitleLanguages = subtitleLanguages;

            //logger.Trace(" XXXX 10");
            contract.Stat_AllVideoQuality = RepoFactory.Adhoc.GetAllVideoQualityForAnime(session, this.AnimeID);

            AnimeVideoQualityStat stat = RepoFactory.Adhoc.GetEpisodeVideoQualityStatsForAnime(session, this.AnimeID);
            contract.Stat_AllVideoQuality_Episodes = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            if (stat != null && stat.VideoQualityEpisodeCount.Count > 0)
            {
                foreach (KeyValuePair<string, int> kvp in stat.VideoQualityEpisodeCount)
                {
                    if (kvp.Value >= EpisodeCountNormal)
                    {
                        contract.Stat_AllVideoQuality_Episodes.Add(kvp.Key);
                    }
                }
            }

            //logger.Trace(" XXXX 11");

            Contract = contract;
        }


        public AnimeFull ToContractAzure()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return ToContractAzure(session.Wrap());
            }
        }

        public AnimeFull ToContractAzure(ISessionWrapper session)
        {
            AnimeFull contract = new AnimeFull();
            contract.Detail = new AnimeDetail();
            contract.Characters = new List<AnimeCharacter>();
            contract.Comments = new List<AnimeComment>();

            contract.Detail.AllTags = this.TagsString;
            contract.Detail.AllCategories = this.TagsString;
            contract.Detail.AnimeID = this.AnimeID;
            contract.Detail.AnimeName = this.MainTitle;
            contract.Detail.AnimeType = this.AnimeTypeDescription;
            contract.Detail.Description = this.Description;
            contract.Detail.EndDateLong = Utils.GetAniDBDateAsSeconds(this.EndDate);
            contract.Detail.StartDateLong = Utils.GetAniDBDateAsSeconds(this.AirDate);
            contract.Detail.EpisodeCountNormal = this.EpisodeCountNormal;
            contract.Detail.EpisodeCountSpecial = this.EpisodeCountSpecial;
            contract.Detail.FanartURL = GetDefaultFanartOnlineURL(session);
            contract.Detail.OverallRating = this.AniDBRating;
            contract.Detail.PosterURL = String.Format(Constants.URLS.AniDB_Images, Picname);
            contract.Detail.TotalVotes = this.AniDBTotalVotes;


            List<AniDB_Anime_Character> animeChars = RepoFactory.AniDB_Anime_Character.GetByAnimeID(session, AnimeID);

            if (animeChars != null || animeChars.Count > 0)
            {
                // first get all the main characters
                foreach (
                    AniDB_Anime_Character animeChar in
                        animeChars.Where(
                            item =>
                                item.CharType.Equals("main character in", StringComparison.InvariantCultureIgnoreCase)))
                {
                    AniDB_Character chr = RepoFactory.AniDB_Character.GetByCharID(session, animeChar.CharID);
                    if (chr != null)
                        contract.Characters.Add(chr.ToContractAzure(animeChar));
                }

                // now get the rest
                foreach (
                    AniDB_Anime_Character animeChar in
                        animeChars.Where(
                            item =>
                                !item.CharType.Equals("main character in", StringComparison.InvariantCultureIgnoreCase))
                    )
                {
                    AniDB_Character chr = RepoFactory.AniDB_Character.GetByCharID(session, animeChar.CharID);
                    if (chr != null)
                        contract.Characters.Add(chr.ToContractAzure(animeChar));
                }
            }


            foreach (AniDB_Recommendation rec in RepoFactory.AniDB_Recommendation.GetByAnimeID(session, AnimeID))
            {
                AnimeComment comment = new AnimeComment();

                comment.UserID = rec.UserID;
                comment.UserName = "";

                // Comment details
                comment.CommentText = rec.RecommendationText;
                comment.IsSpoiler = false;
                comment.CommentDateLong = 0;

                comment.ImageURL = String.Empty;

                AniDBRecommendationType recType = (AniDBRecommendationType) rec.RecommendationType;
                switch (recType)
                {
                    case AniDBRecommendationType.ForFans:
                        comment.CommentType = (int) WhatPeopleAreSayingType.AniDBForFans;
                        break;
                    case AniDBRecommendationType.MustSee:
                        comment.CommentType = (int) WhatPeopleAreSayingType.AniDBMustSee;
                        break;
                    case AniDBRecommendationType.Recommended:
                        comment.CommentType = (int) WhatPeopleAreSayingType.AniDBRecommendation;
                        break;
                }

                comment.Source = "AniDB";
                contract.Comments.Add(comment);
            }

            return contract;
        }

        public AnimeSeries CreateAnimeSeriesAndGroup()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return CreateAnimeSeriesAndGroup(session);
            }
        }

        public AnimeSeries CreateAnimeSeriesAndGroup(ISession session)
        {
            // create a new AnimeSeries record
           
            AnimeSeries ser = new AnimeSeries();
            ser.Populate(this);

            ISessionWrapper sessionWrapper = session.Wrap();


            // create the AnimeGroup record
            // check if there are any existing groups we could add this series to
            bool createNewGroup = true;

            if (ServerSettings.AutoGroupSeries)
            {
                List<AnimeGroup> grps = AnimeGroup.GetRelatedGroupsFromAnimeID(sessionWrapper, ser.AniDB_ID, true);

                // only use if there is just one result

                // we are moving all to a single group, and then naming it, so keep outside loop
                AnimeSeries name = null;
                string customGroupName = null;
                if (grps != null && grps.Count > 0)
                {
                    int groupID = -1;
                    foreach (AnimeGroup grp in grps.ToList())
                        //FIX repSeries.Save(series, true) Groups might change the enumeration
                    {
                        bool groupHasCustomName = true;
                        if (grp.GroupName.Equals("AAA Migrating Groups AAA")) continue;
                        if (groupID == -1) groupID = grp.AnimeGroupID;
                        ser.AnimeGroupID = groupID;
                        if (name == null) name = ser;

                        #region Naming

                        if (grp.DefaultAnimeSeriesID.HasValue)
                        {
                            name = RepoFactory.AnimeSeries.GetByID(grp.DefaultAnimeSeriesID.Value);
                            if (name == null)
                            {
                                grp.DefaultAnimeSeriesID = null;
                                //TODO what this means, its not saved back to AnimeGroup
                            }
                            else
                            {
                                groupHasCustomName = false;
                            }
                        }
                        foreach (AnimeSeries series in grp.GetAllSeries())
                        {
                            series.AnimeGroupID = groupID;
                            RepoFactory.AnimeSeries.Save(series, true);

                            if (!grp.DefaultAnimeSeriesID.HasValue)
                            {
                                if (name == null)
                                {
                                    name = series;
                                }
                                else
                                {
                                    if (series.AirDate < name.AirDate)
                                    {
                                        name = series;
                                    }
                                }
                                // Check all titles for custom naming, in case user changed language preferences
                                if (series.SeriesNameOverride.Equals(grp.GroupName))
                                {
                                    groupHasCustomName = false;
                                }
                                else
                                {
                                    foreach (AniDB_Anime_Title title in series.GetAnime().GetTitles())
                                    {
                                        if (title.Title.Equals(grp.GroupName))
                                        {
                                            groupHasCustomName = false;
                                            break;
                                        }
                                    }

									#region tvdb names
									List<TvDB_Series> tvdbs = series.GetTvDBSeries();
									if (tvdbs != null && tvdbs.Count != 0)
									{
										foreach (TvDB_Series tvdbser in tvdbs)
										{
											if (tvdbser.SeriesName.Equals(grp.GroupName))
											{
												groupHasCustomName = false;
												break;
											}
										}
									}
									#endregion
								}
							}
                        }

                        if (groupHasCustomName)
                            customGroupName = grp.GroupName;
                    }
                    if (name != null)
                    {
                        AnimeGroup newGroup = RepoFactory.AnimeGroup.GetByID(groupID);
						string newTitle = name.GetSeriesName();
						if (newGroup.DefaultAnimeSeriesID.HasValue &&
							newGroup.DefaultAnimeSeriesID.Value != name.AnimeSeriesID)
							newTitle = RepoFactory.AnimeSeries.GetByID(newGroup.DefaultAnimeSeriesID.Value).GetSeriesName();
						if (customGroupName != null) newTitle = customGroupName;
						// reset tags, description, etc to new series
						newGroup.Populate(name, DateTime.Now);
                        newGroup.GroupName = newTitle;
                        newGroup.SortName = newTitle;
                        RepoFactory.AnimeGroup.Save(newGroup, true, true);
                    }

                    #endregion

                    createNewGroup = false;
                    foreach (AnimeGroup group in grps)
                    {
                        if (group.GetAllSeries().Count == 0) RepoFactory.AnimeGroup.Delete(group.AnimeGroupID);
                    }
                }
            }

            if (createNewGroup)
            {
                AnimeGroup anGroup = new AnimeGroup();
                anGroup.Populate(ser, DateTime.Now);
                RepoFactory.AnimeGroup.Save(anGroup, true, true);

                ser.AnimeGroupID = anGroup.AnimeGroupID;
            }

            RepoFactory.AnimeSeries.Save(ser, false, false);

            // check for TvDB associations
            CommandRequest_TvDBSearchAnime cmd = new CommandRequest_TvDBSearchAnime(this.AnimeID, false);
            cmd.Save();

            // check for Trakt associations
            if (ServerSettings.Trakt_IsEnabled && !String.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
            {
                CommandRequest_TraktSearchAnime cmd2 = new CommandRequest_TraktSearchAnime(this.AnimeID, false);
                cmd2.Save();
            }

            return ser;
        }

        public static void GetRelatedAnimeRecursive(ISessionWrapper session, int animeID, ref List<AniDB_Anime> relList,
            ref List<int> relListIDs, ref List<int> searchedIDs)
        {
            AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            searchedIDs.Add(animeID);

            foreach (AniDB_Anime_Relation rel in anime.GetRelatedAnime(session))
            {
                string relationtype = rel.RelationType.ToLower();
                if (AnimeGroup.IsRelationTypeInExclusions(relationtype))
                {
                    //Filter these relations these will fix messes, like Gundam , Clamp, etc.
                    continue;
                }
                AniDB_Anime relAnime = RepoFactory.AniDB_Anime.GetByAnimeID(session, rel.RelatedAnimeID);
                if (relAnime != null && !relListIDs.Contains(relAnime.AnimeID))
                {
	                if(AnimeGroup.IsRelationTypeInExclusions(relAnime.AnimeTypeDescription.ToLower())) continue;
                    relList.Add(relAnime);
                    relListIDs.Add(relAnime.AnimeID);
                    if (!searchedIDs.Contains(rel.RelatedAnimeID))
                    {
                        GetRelatedAnimeRecursive(session, rel.RelatedAnimeID, ref relList, ref relListIDs,
                            ref searchedIDs);
                    }
                }
            }
        }

        public static void UpdateStatsByAnimeID(int id)
        {
            AniDB_Anime an = RepoFactory.AniDB_Anime.GetByAnimeID(id);
            if (an != null)
                RepoFactory.AniDB_Anime.Save(an);
            AnimeSeries series = RepoFactory.AnimeSeries.GetByAnimeID(id);
            if (series != null)
                // Update more than just stats in case the xrefs have changed
                RepoFactory.AnimeSeries.Save(series, true, false, false, true);
        }
    }
}