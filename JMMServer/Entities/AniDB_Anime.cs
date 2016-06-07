using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using AniDBAPI;
using BinaryNorthwest;
using JMMContracts;
using JMMServer.Commands;
using JMMServer.ImageDownload;
using JMMServer.Properties;
using JMMServer.Providers.Azure;
using JMMServer.Repositories;
using NHibernate;
using NHibernate.Criterion;
using NLog;

namespace JMMServer.Entities
{
    public class AniDB_Anime
    {
        public const int LastYear = 2050;

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private Dictionary<int, TvDB_Episode> dictTvDBEpisodes;

        private Dictionary<int, int> dictTvDBSeasons;

        private Dictionary<int, int> dictTvDBSeasonsSpecials;

        // these files come from AniDB but we don't directly save them
        private string reviewIDListRAW;

        public AniDB_Anime()
        {
            DisableExternalLinksFlag = 0;
        }

        public enAnimeType AnimeTypeEnum
        {
            get
            {
                if (AnimeType > 5) return enAnimeType.Other;
                return (enAnimeType)AnimeType;
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

        [XmlIgnore]
        public string PosterPath
        {
            get
            {
                if (string.IsNullOrEmpty(Picname)) return "";

                return Path.Combine(ImageUtils.GetAniDBImagePath(AnimeID), Picname);
            }
        }

        [XmlIgnore]
        public string Year
        {
            get
            {
                var y = BeginYear.ToString();
                if (BeginYear != EndYear)
                {
                    if (EndYear == LastYear)
                        y += "-Ongoing";
                    else
                        y += "-" + EndYear;
                }
                return y;
            }
        }

        public string PosterPathNoDefault
        {
            get
            {
                var fileName = Path.Combine(ImageUtils.GetAniDBImagePath(AnimeID), Picname);
                return fileName;
            }
        }

        public string AnimeTypeRAW
        {
            get
            {
                switch (AnimeType)
                {
                    case (int)AnimeTypes.Movie:
                        return "movie";
                    case (int)AnimeTypes.OVA:
                        return "ova";
                    case (int)AnimeTypes.TV_Series:
                        return "tv series";
                    case (int)AnimeTypes.TV_Special:
                        return "tv special";
                    case (int)AnimeTypes.Web:
                        return "web";
                    default:
                        return "other";
                }
            }
            set
            {
                switch (value.ToLower())
                {
                    case "movie":
                        AnimeType = (int)AnimeTypes.Movie;
                        break;
                    case "ova":
                        AnimeType = (int)AnimeTypes.OVA;
                        break;
                    case "tv series":
                        AnimeType = (int)AnimeTypes.TV_Series;
                        break;
                    case "tv special":
                        AnimeType = (int)AnimeTypes.TV_Special;
                        break;
                    case "web":
                        AnimeType = (int)AnimeTypes.Web;
                        break;
                    default:
                        AnimeType = (int)AnimeTypes.Other;
                        break;
                }
            }
        }

        [XmlIgnore]
        public string AnimeTypeName
        {
            get { return Enum.GetName(typeof(AnimeTypes), (AnimeTypes)AnimeType).Replace('_', ' '); }
        }

        [XmlIgnore]
        public string TagsString
        {
            get
            {
                var tags = GetTags();
                var temp = "";
                foreach (var tag in tags)
                    temp += tag.TagName + "|";
                if (temp.Length > 2)
                    temp = temp.Substring(0, temp.Length - 2);
                return temp;
            }
        }


        [XmlIgnore]
        public bool SearchOnTvDB
        {
            get { return AnimeType != (int)AnimeTypes.Movie; }
        }

        [XmlIgnore]
        public bool SearchOnMovieDB
        {
            get { return AnimeType == (int)AnimeTypes.Movie; }
        }

        [XmlIgnore]
        public List<AniDB_Anime_Review> AnimeReviews
        {
            get
            {
                var RepRevs = new AniDB_Anime_ReviewRepository();
                return RepRevs.GetByAnimeID(AnimeID);
            }
        }

        public decimal AniDBRating
        {
            get
            {
                try
                {
                    if (AniDBTotalVotes == 0)
                        return 0;
                    return AniDBTotalRating / AniDBTotalVotes;
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
                    totalRating += (decimal)Rating * VoteCount;
                    totalRating += (decimal)TempRating * TempVoteCount;

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

        [XmlIgnore]
        public AniDB_Vote UserVote
        {
            get
            {
                try
                {
                    var repVotes = new AniDB_VoteRepository();
                    var dbVote = repVotes.GetByAnimeID(AnimeID);
                    return dbVote;
                }
                catch (Exception ex)
                {
                    logger.Error("Error in  UserVote: {0}", ex.ToString());
                    return null;
                }
            }
        }


        public string PreferredTitle
        {
            get
            {
                var titles = GetTitles();

                foreach (var nlan in Languages.PreferredNamingLanguages)
                {
                    var thisLanguage = nlan.Language.Trim().ToUpper();
                    // Romaji and English titles will be contained in MAIN and/or OFFICIAL
                    // we won't use synonyms for these two languages
                    if (thisLanguage == "X-JAT" || thisLanguage == "EN")
                    {
                        // first try the  Main title
                        for (var i = 0; i < titles.Count; i++)
                        {
                            if (titles[i].Language.Trim().ToUpper() == thisLanguage &&
                                titles[i].TitleType.Trim().ToUpper() == Constants.AnimeTitleType.Main.ToUpper())
                                return titles[i].Title;
                        }
                    }

                    // now try the official title
                    for (var i = 0; i < titles.Count; i++)
                    {
                        if (titles[i].Language.Trim().ToUpper() == thisLanguage &&
                            titles[i].TitleType.Trim().ToUpper() == Constants.AnimeTitleType.Official.ToUpper())
                            return titles[i].Title;
                    }

                    // try synonyms
                    if (ServerSettings.LanguageUseSynonyms)
                    {
                        for (var i = 0; i < titles.Count; i++)
                        {
                            if (titles[i].Language.Trim().ToUpper() == thisLanguage &&
                                titles[i].TitleType.Trim().ToUpper() == Constants.AnimeTitleType.Synonym.ToUpper())
                                return titles[i].Title;
                        }
                    }
                }

                // otherwise just use the main title
                for (var i = 0; i < titles.Count; i++)
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
                var repEps = new AniDB_EpisodeRepository();
                return repEps.GetByAnimeID(AnimeID);
            }
        }

        public List<TvDB_Episode> GetTvDBEpisodes()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetTvDBEpisodes(session);
            }
        }

        public List<TvDB_Episode> GetTvDBEpisodes(ISession session)
        {
            var tvDBEpisodes = new List<TvDB_Episode>();

            var xrefs = GetCrossRefTvDBV2(session);
            if (xrefs.Count == 0) return tvDBEpisodes;

            var repEps = new TvDB_EpisodeRepository();
            foreach (var xref in xrefs)
            {
                tvDBEpisodes.AddRange(repEps.GetBySeriesID(session, xref.TvDBID));
            }

            var sortCriteria = new List<SortPropOrFieldAndDirection>();
            sortCriteria.Add(new SortPropOrFieldAndDirection("SeasonNumber", false, SortType.eInteger));
            sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeNumber", false, SortType.eInteger));
            tvDBEpisodes = Sorting.MultiSort(tvDBEpisodes, sortCriteria);

            return tvDBEpisodes;
        }

        public Dictionary<int, TvDB_Episode> GetDictTvDBEpisodes()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetDictTvDBEpisodes(session);
            }
        }

        public Dictionary<int, TvDB_Episode> GetDictTvDBEpisodes(ISession session)
        {
            if (dictTvDBEpisodes == null)
            {
                try
                {
                    var tvdbEpisodes = GetTvDBEpisodes(session);
                    if (tvdbEpisodes != null)
                    {
                        dictTvDBEpisodes = new Dictionary<int, TvDB_Episode>();
                        // create a dictionary of absolute episode numbers for tvdb episodes
                        // sort by season and episode number
                        // ignore season 0, which is used for specials
                        var eps = tvdbEpisodes;

                        var i = 1;
                        foreach (var ep in eps)
                        {
                            dictTvDBEpisodes[i] = ep;
                            i++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.ErrorException(ex.ToString(), ex);
                }
            }
            return dictTvDBEpisodes;
        }

        public Dictionary<int, int> GetDictTvDBSeasons()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetDictTvDBSeasons(session);
            }
        }

        public Dictionary<int, int> GetDictTvDBSeasons(ISession session)
        {
            if (dictTvDBSeasons == null)
            {
                try
                {
                    var tvdbEpisodes = GetTvDBEpisodes(session);
                    if (tvdbEpisodes != null)
                    {
                        dictTvDBSeasons = new Dictionary<int, int>();
                        // create a dictionary of season numbers and the first episode for that season

                        var eps = tvdbEpisodes;
                        var i = 1;
                        var lastSeason = -999;
                        foreach (var ep in eps)
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
                    logger.ErrorException(ex.ToString(), ex);
                }
            }
            return dictTvDBSeasons;
        }

        public Dictionary<int, int> GetDictTvDBSeasonsSpecials()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetDictTvDBSeasonsSpecials(session);
            }
        }

        public Dictionary<int, int> GetDictTvDBSeasonsSpecials(ISession session)
        {
            if (dictTvDBSeasonsSpecials == null)
            {
                try
                {
                    var tvdbEpisodes = GetTvDBEpisodes(session);
                    if (tvdbEpisodes != null)
                    {
                        dictTvDBSeasonsSpecials = new Dictionary<int, int>();
                        // create a dictionary of season numbers and the first episode for that season

                        var eps = tvdbEpisodes;
                        var i = 1;
                        var lastSeason = -999;
                        foreach (var ep in eps)
                        {
                            if (ep.SeasonNumber > 0) continue;

                            var thisSeason = 0;
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
                    logger.ErrorException(ex.ToString(), ex);
                }
            }
            return dictTvDBSeasonsSpecials;
        }

        public List<CrossRef_AniDB_TvDB_Episode> GetCrossRefTvDBEpisodes()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetCrossRefTvDBEpisodes(session);
            }
        }

        public List<CrossRef_AniDB_TvDB_Episode> GetCrossRefTvDBEpisodes(ISession session)
        {
            var repCrossRef = new CrossRef_AniDB_TvDB_EpisodeRepository();
            return repCrossRef.GetByAnimeID(session, AnimeID);
        }

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
            return repCrossRef.GetByAnimeID(session, AnimeID);
        }

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
            return repCrossRef.GetByAnimeID(session, AnimeID);
        }

        public List<CrossRef_AniDB_MAL> GetCrossRefMAL()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetCrossRefMAL(session);
            }
        }

        public List<CrossRef_AniDB_MAL> GetCrossRefMAL(ISession session)
        {
            var repCrossRef = new CrossRef_AniDB_MALRepository();
            return repCrossRef.GetByAnimeID(session, AnimeID);
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
            var repSeries = new TvDB_SeriesRepository();

            var ret = new List<TvDB_Series>();
            var xrefs = GetCrossRefTvDBV2(session);
            if (xrefs.Count == 0) return ret;

            foreach (var xref in xrefs)
            {
                var ser = repSeries.GetByTvDBID(session, xref.TvDBID);
                if (ser != null) ret.Add(ser);
            }

            return ret;
        }

        public List<TvDB_ImageFanart> GetTvDBImageFanarts()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetTvDBImageFanarts(session);
            }
        }

        public List<TvDB_ImageFanart> GetTvDBImageFanarts(ISession session)
        {
            var ret = new List<TvDB_ImageFanart>();

            var xrefs = GetCrossRefTvDBV2(session);
            if (xrefs.Count == 0) return ret;

            var repFanart = new TvDB_ImageFanartRepository();
            foreach (var xref in xrefs)
            {
                ret.AddRange(repFanart.GetBySeriesID(session, xref.TvDBID));
            }


            return ret;
        }

        public List<TvDB_ImagePoster> GetTvDBImagePosters()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetTvDBImagePosters(session);
            }
        }

        public List<TvDB_ImagePoster> GetTvDBImagePosters(ISession session)
        {
            var ret = new List<TvDB_ImagePoster>();

            var xrefs = GetCrossRefTvDBV2(session);
            if (xrefs.Count == 0) return ret;

            var repPosters = new TvDB_ImagePosterRepository();

            foreach (var xref in xrefs)
            {
                ret.AddRange(repPosters.GetBySeriesID(session, xref.TvDBID));
            }

            return ret;
        }

        public List<TvDB_ImageWideBanner> GetTvDBImageWideBanners()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetTvDBImageWideBanners(session);
            }
        }

        public List<TvDB_ImageWideBanner> GetTvDBImageWideBanners(ISession session)
        {
            var ret = new List<TvDB_ImageWideBanner>();

            var xrefs = GetCrossRefTvDBV2(session);
            if (xrefs.Count == 0) return ret;

            var repBanners = new TvDB_ImageWideBannerRepository();
            foreach (var xref in xrefs)
            {
                ret.AddRange(repBanners.GetBySeriesID(xref.TvDBID));
            }
            return ret;
        }

        public CrossRef_AniDB_Other GetCrossRefMovieDB()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetCrossRefMovieDB(session);
            }
        }

        public CrossRef_AniDB_Other GetCrossRefMovieDB(ISession session)
        {
            var repCrossRef = new CrossRef_AniDB_OtherRepository();
            return repCrossRef.GetByAnimeIDAndType(session, AnimeID, CrossRefType.MovieDB);
        }


        public MovieDB_Movie GetMovieDBMovie()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetMovieDBMovie(session);
            }
        }

        public MovieDB_Movie GetMovieDBMovie(ISession session)
        {
            var xref = GetCrossRefMovieDB(session);
            if (xref == null) return null;

            var repMovies = new MovieDB_MovieRepository();
            return repMovies.GetByOnlineID(session, int.Parse(xref.CrossRefID));
        }

        public List<MovieDB_Fanart> GetMovieDBFanarts()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetMovieDBFanarts(session);
            }
        }

        public List<MovieDB_Fanart> GetMovieDBFanarts(ISession session)
        {
            var xref = GetCrossRefMovieDB(session);
            if (xref == null) return new List<MovieDB_Fanart>();

            var repFanart = new MovieDB_FanartRepository();
            return repFanart.GetByMovieID(session, int.Parse(xref.CrossRefID));
        }

        public List<MovieDB_Poster> GetMovieDBPosters()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetMovieDBPosters(session);
            }
        }

        public List<MovieDB_Poster> GetMovieDBPosters(ISession session)
        {
            var xref = GetCrossRefMovieDB(session);
            if (xref == null) return new List<MovieDB_Poster>();

            var repPosters = new MovieDB_PosterRepository();
            return repPosters.GetByMovieID(session, int.Parse(xref.CrossRefID));
        }

        public AniDB_Anime_DefaultImage GetDefaultPoster()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetDefaultPoster(session);
            }
        }

        public AniDB_Anime_DefaultImage GetDefaultPoster(ISession session)
        {
            var repDefaults = new AniDB_Anime_DefaultImageRepository();
            return repDefaults.GetByAnimeIDAndImagezSizeType(session, AnimeID, (int)ImageSizeType.Poster);
        }

        public string GetDefaultPosterPathNoBlanks()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetDefaultPosterPathNoBlanks(session);
            }
        }

        public string GetDefaultPosterPathNoBlanks(ISession session)
        {
            var defaultPoster = GetDefaultPoster(session);
            if (defaultPoster == null)
                return PosterPathNoDefault;
            var imageType = (ImageEntityType)defaultPoster.ImageParentType;

            switch (imageType)
            {
                case ImageEntityType.AniDB_Cover:
                    return PosterPath;

                case ImageEntityType.TvDB_Cover:

                    var repTvPosters = new TvDB_ImagePosterRepository();
                    var tvPoster = repTvPosters.GetByID(session, defaultPoster.ImageParentID);
                    if (tvPoster != null)
                        return tvPoster.FullImagePath;
                    return PosterPath;

                case ImageEntityType.Trakt_Poster:

                    var repTraktPosters = new Trakt_ImagePosterRepository();
                    var traktPoster = repTraktPosters.GetByID(session, defaultPoster.ImageParentID);
                    if (traktPoster != null)
                        return traktPoster.FullImagePath;
                    return PosterPath;

                case ImageEntityType.MovieDB_Poster:

                    var repMoviePosters = new MovieDB_PosterRepository();
                    var moviePoster = repMoviePosters.GetByID(session, defaultPoster.ImageParentID);
                    if (moviePoster != null)
                        return moviePoster.FullImagePath;
                    return PosterPath;
            }

            return PosterPath;
        }

        public ImageDetails GetDefaultPosterDetailsNoBlanks()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetDefaultPosterDetailsNoBlanks(session);
            }
        }

        public ImageDetails GetDefaultPosterDetailsNoBlanks(ISession session)
        {
            var details = new ImageDetails { ImageType = JMMImageType.AniDB_Cover, ImageID = AnimeID };
            var defaultPoster = GetDefaultPoster(session);

            if (defaultPoster == null)
                return details;
            var imageType = (ImageEntityType)defaultPoster.ImageParentType;

            switch (imageType)
            {
                case ImageEntityType.AniDB_Cover:
                    return details;

                case ImageEntityType.TvDB_Cover:

                    var repTvPosters = new TvDB_ImagePosterRepository();
                    var tvPoster = repTvPosters.GetByID(session, defaultPoster.ImageParentID);
                    if (tvPoster != null)
                        details = new ImageDetails
                        {
                            ImageType = JMMImageType.TvDB_Cover,
                            ImageID = tvPoster.TvDB_ImagePosterID
                        };

                    return details;

                case ImageEntityType.Trakt_Poster:

                    var repTraktPosters = new Trakt_ImagePosterRepository();
                    var traktPoster = repTraktPosters.GetByID(session, defaultPoster.ImageParentID);
                    if (traktPoster != null)
                        details = new ImageDetails
                        {
                            ImageType = JMMImageType.Trakt_Poster,
                            ImageID = traktPoster.Trakt_ImagePosterID
                        };

                    return details;

                case ImageEntityType.MovieDB_Poster:

                    var repMoviePosters = new MovieDB_PosterRepository();
                    var moviePoster = repMoviePosters.GetByID(session, defaultPoster.ImageParentID);
                    if (moviePoster != null)
                        details = new ImageDetails
                        {
                            ImageType = JMMImageType.MovieDB_Poster,
                            ImageID = moviePoster.MovieDB_PosterID
                        };

                    return details;
            }

            return details;
        }

        public AniDB_Anime_DefaultImage GetDefaultFanart()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetDefaultFanart(session);
            }
        }

        public AniDB_Anime_DefaultImage GetDefaultFanart(ISession session)
        {
            var repDefaults = new AniDB_Anime_DefaultImageRepository();
            return repDefaults.GetByAnimeIDAndImagezSizeType(session, AnimeID, (int)ImageSizeType.Fanart);
        }

        public ImageDetails GetDefaultFanartDetailsNoBlanks()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetDefaultFanartDetailsNoBlanks(session);
            }
        }

        public ImageDetails GetDefaultFanartDetailsNoBlanks(ISession session)
        {
            var fanartRandom = new Random();

            ImageDetails details = null;
            if (GetDefaultFanart() == null)
            {
                // get a random fanart (only tvdb)
                if (AnimeTypeEnum == enAnimeType.Movie)
                {
                    var fanarts = GetMovieDBFanarts(session);
                    if (fanarts.Count == 0) return null;

                    var movieFanart = fanarts[fanartRandom.Next(0, fanarts.Count)];
                    details = new ImageDetails
                    {
                        ImageType = JMMImageType.MovieDB_FanArt,
                        ImageID = movieFanart.MovieDB_FanartID
                    };
                    return details;
                }
                else
                {
                    var fanarts = GetTvDBImageFanarts(session);
                    if (fanarts.Count == 0) return null;

                    var tvFanart = fanarts[fanartRandom.Next(0, fanarts.Count)];
                    details = new ImageDetails
                    {
                        ImageType = JMMImageType.TvDB_FanArt,
                        ImageID = tvFanart.TvDB_ImageFanartID
                    };
                    return details;
                }
            }
            var imageType = (ImageEntityType)GetDefaultFanart().ImageParentType;

            switch (imageType)
            {
                case ImageEntityType.TvDB_FanArt:

                    var repTvFanarts = new TvDB_ImageFanartRepository();
                    var tvFanart = repTvFanarts.GetByID(session, GetDefaultFanart(session).ImageParentID);
                    if (tvFanart != null)
                        details = new ImageDetails
                        {
                            ImageType = JMMImageType.TvDB_FanArt,
                            ImageID = tvFanart.TvDB_ImageFanartID
                        };

                    return details;

                case ImageEntityType.Trakt_Fanart:

                    var repTraktFanarts = new Trakt_ImageFanartRepository();
                    var traktFanart = repTraktFanarts.GetByID(session, GetDefaultFanart(session).ImageParentID);
                    if (traktFanart != null)
                        details = new ImageDetails
                        {
                            ImageType = JMMImageType.Trakt_Fanart,
                            ImageID = traktFanart.Trakt_ImageFanartID
                        };

                    return details;

                case ImageEntityType.MovieDB_FanArt:

                    var repMovieFanarts = new MovieDB_FanartRepository();
                    var movieFanart = repMovieFanarts.GetByID(session, GetDefaultFanart(session).ImageParentID);
                    if (movieFanart != null)
                        details = new ImageDetails
                        {
                            ImageType = JMMImageType.MovieDB_FanArt,
                            ImageID = movieFanart.MovieDB_FanartID
                        };

                    return details;
            }

            return null;
        }

        public string GetDefaultFanartOnlineURL()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetDefaultFanartOnlineURL(session);
            }
        }

        public string GetDefaultFanartOnlineURL(ISession session)
        {
            var fanartRandom = new Random();


            if (GetDefaultFanart() == null)
            {
                // get a random fanart
                if (AnimeTypeEnum == enAnimeType.Movie)
                {
                    var fanarts = GetMovieDBFanarts(session);
                    if (fanarts.Count == 0) return "";

                    var movieFanart = fanarts[fanartRandom.Next(0, fanarts.Count)];
                    return movieFanart.URL;
                }
                else
                {
                    var fanarts = GetTvDBImageFanarts(session);
                    if (fanarts.Count == 0) return null;

                    var tvFanart = fanarts[fanartRandom.Next(0, fanarts.Count)];
                    return string.Format(Constants.URLS.TvDB_Images, tvFanart.BannerPath);
                }
            }
            var imageType = (ImageEntityType)GetDefaultFanart().ImageParentType;

            switch (imageType)
            {
                case ImageEntityType.TvDB_FanArt:

                    var repTvFanarts = new TvDB_ImageFanartRepository();
                    var tvFanart = repTvFanarts.GetByID(GetDefaultFanart(session).ImageParentID);
                    if (tvFanart != null)
                        return string.Format(Constants.URLS.TvDB_Images, tvFanart.BannerPath);

                    break;

                case ImageEntityType.Trakt_Fanart:

                    var repTraktFanarts = new Trakt_ImageFanartRepository();
                    var traktFanart = repTraktFanarts.GetByID(GetDefaultFanart(session).ImageParentID);
                    if (traktFanart != null)
                        return traktFanart.ImageURL;

                    break;

                case ImageEntityType.MovieDB_FanArt:

                    var repMovieFanarts = new MovieDB_FanartRepository();
                    var movieFanart = repMovieFanarts.GetByID(GetDefaultFanart(session).ImageParentID);
                    if (movieFanart != null)
                        return movieFanart.URL;

                    break;
            }

            return "";
        }

        public AniDB_Anime_DefaultImage GetDefaultWideBanner()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetDefaultWideBanner(session);
            }
        }

        public AniDB_Anime_DefaultImage GetDefaultWideBanner(ISession session)
        {
            var repDefaults = new AniDB_Anime_DefaultImageRepository();
            return repDefaults.GetByAnimeIDAndImagezSizeType(session, AnimeID, (int)ImageSizeType.WideBanner);
        }

        public List<AniDB_Tag> GetTags()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetTags(session);
            }
        }

        public List<AniDB_Tag> GetTags(ISession session)
        {
            var repTag = new AniDB_TagRepository();

            var tags = new List<AniDB_Tag>();
            foreach (var tag in GetAnimeTags(session))
            {
                var newTag = repTag.GetByTagID(tag.TagID, session);
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

        public List<CustomTag> GetCustomTagsForAnime(ISession session)
        {
            var repTags = new CustomTagRepository();
            return repTags.GetByAnimeID(session, AnimeID);
        }

        public List<AniDB_Tag> GetAniDBTags(ISession session)
        {
            var repTags = new AniDB_TagRepository();
            return repTags.GetByAnimeID(session, AnimeID);
        }

        public List<AniDB_Anime_Tag> GetAnimeTags(ISession session)
        {
            var repAnimeTags = new AniDB_Anime_TagRepository();
            return repAnimeTags.GetByAnimeID(session, AnimeID);
        }

        public List<AniDB_Anime_Relation> GetRelatedAnime()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetRelatedAnime(session);
            }
        }

        public List<AniDB_Anime_Relation> GetRelatedAnime(ISession session)
        {
            var repRels = new AniDB_Anime_RelationRepository();
            return repRels.GetByAnimeID(session, AnimeID);
        }

        public List<AniDB_Anime_Similar> GetSimilarAnime()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetSimilarAnime(session);
            }
        }

        public List<AniDB_Anime_Similar> GetSimilarAnime(ISession session)
        {
            var rep = new AniDB_Anime_SimilarRepository();
            return rep.GetByAnimeID(session, AnimeID);
        }

        public List<AniDB_Anime> GetAllRelatedAnime()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetAllRelatedAnime(session);
            }
        }

        public List<AniDB_Anime> GetAllRelatedAnime(ISession session)
        {
            var relList = new List<AniDB_Anime>();
            var relListIDs = new List<int>();
            var searchedIDs = new List<int>();

            GetRelatedAnimeRecursive(session, AnimeID, ref relList, ref relListIDs, ref searchedIDs);
            return relList;
        }

        public List<AniDB_Anime_Character> GetAnimeCharacters()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetAnimeCharacters(session);
            }
        }

        public List<AniDB_Anime_Character> GetAnimeCharacters(ISession session)
        {
            var repRels = new AniDB_Anime_CharacterRepository();
            return repRels.GetByAnimeID(session, AnimeID);
        }

        public List<AniDB_Anime_Title> GetTitles()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetTitles(session);
            }
        }

        public List<AniDB_Anime_Title> GetTitles(ISession session)
        {
            var repTitles = new AniDB_Anime_TitleRepository();
            return repTitles.GetByAnimeID(session, AnimeID);
        }

        public string GetFormattedTitle(List<AniDB_Anime_Title> titles)
        {
            foreach (var nlan in Languages.PreferredNamingLanguages)
            {
                var thisLanguage = nlan.Language.Trim().ToUpper();

                // Romaji and English titles will be contained in MAIN and/or OFFICIAL
                // we won't use synonyms for these two languages
                if (thisLanguage.Equals(Constants.AniDBLanguageType.Romaji) ||
                    thisLanguage.Equals(Constants.AniDBLanguageType.English))
                {
                    foreach (var title in titles)
                    {
                        var titleType = title.TitleType.Trim().ToUpper();
                        // first try the  Main title
                        if (titleType == Constants.AnimeTitleType.Main.ToUpper() &&
                            title.Language.Trim().ToUpper() == thisLanguage) return title.Title;
                    }
                }

                // now try the official title
                foreach (var title in titles)
                {
                    var titleType = title.TitleType.Trim().ToUpper();
                    if (titleType == Constants.AnimeTitleType.Official.ToUpper() &&
                        title.Language.Trim().ToUpper() == thisLanguage) return title.Title;
                }

                // try synonyms
                if (ServerSettings.LanguageUseSynonyms)
                {
                    foreach (var title in titles)
                    {
                        var titleType = title.TitleType.Trim().ToUpper();
                        if (titleType == Constants.AnimeTitleType.Synonym.ToUpper() &&
                            title.Language.Trim().ToUpper() == thisLanguage) return title.Title;
                    }
                }
            }

            // otherwise just use the main title
            return MainTitle;
        }

        public string GetFormattedTitle()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetFormattedTitle(session);
            }
        }

        public string GetFormattedTitle(ISession session)
        {
            var thisTitles = GetTitles(session);
            return GetFormattedTitle(thisTitles);
        }

        public AniDB_Vote GetUserVote(ISession session)
        {
            try
            {
                var repVotes = new AniDB_VoteRepository();
                var dbVote = repVotes.GetByAnimeID(session, AnimeID);
                return dbVote;
            }
            catch (Exception ex)
            {
                logger.Error("Error in  UserVote: {0}", ex.ToString());
                return null;
            }
        }

        public List<AniDB_Episode> GetAniDBEpisodes(ISession session)
        {
            var repEps = new AniDB_EpisodeRepository();
            return repEps.GetByAnimeID(session, AnimeID);
        }

        private void Populate(Raw_AniDB_Anime animeInfo)
        {
            AirDate = animeInfo.AirDate;
            AllCinemaID = animeInfo.AllCinemaID;
            AnimeID = animeInfo.AnimeID;
            //this.AnimeNfo = animeInfo.AnimeNfoID;
            AnimePlanetID = animeInfo.AnimePlanetID;
            AnimeTypeRAW = animeInfo.AnimeTypeRAW;
            ANNID = animeInfo.ANNID;
            AvgReviewRating = animeInfo.AvgReviewRating;
            AwardList = animeInfo.AwardList;
            BeginYear = animeInfo.BeginYear;
            DateTimeDescUpdated = DateTime.Now;
            DateTimeUpdated = DateTime.Now;
            Description = animeInfo.Description;
            EndDate = animeInfo.EndDate;
            EndYear = animeInfo.EndYear;
            MainTitle = animeInfo.MainTitle;
            AllTitles = "";
            AllCategories = "";
            AllTags = "";
            //this.EnglishName = animeInfo.EnglishName;
            EpisodeCount = animeInfo.EpisodeCount;
            EpisodeCountNormal = animeInfo.EpisodeCountNormal;
            EpisodeCountSpecial = animeInfo.EpisodeCountSpecial;
            //this.genre
            ImageEnabled = 1;
            //this.KanjiName = animeInfo.KanjiName;
            LatestEpisodeNumber = animeInfo.LatestEpisodeNumber;
            //this.OtherName = animeInfo.OtherName;
            Picname = animeInfo.Picname;
            Rating = animeInfo.Rating;
            //this.relations
            Restricted = animeInfo.Restricted;
            ReviewCount = animeInfo.ReviewCount;
            //this.RomajiName = animeInfo.RomajiName;
            //this.ShortNames = animeInfo.ShortNames.Replace("'", "|");
            //this.Synonyms = animeInfo.Synonyms.Replace("'", "|");
            TempRating = animeInfo.TempRating;
            TempVoteCount = animeInfo.TempVoteCount;
            URL = animeInfo.URL;
            VoteCount = animeInfo.VoteCount;
        }

        public void PopulateAndSaveFromHTTP(ISession session, Raw_AniDB_Anime animeInfo, List<Raw_AniDB_Episode> eps,
            List<Raw_AniDB_Anime_Title> titles,
            List<Raw_AniDB_Category> cats, List<Raw_AniDB_Tag> tags, List<Raw_AniDB_Character> chars,
            List<Raw_AniDB_RelatedAnime> rels, List<Raw_AniDB_SimilarAnime> sims,
            List<Raw_AniDB_Recommendation> recs, bool downloadRelations)
        {
            logger.Trace("------------------------------------------------");
            logger.Trace(string.Format("PopulateAndSaveFromHTTP: for {0} - {1}", animeInfo.AnimeID, animeInfo.MainTitle));
            logger.Trace("------------------------------------------------");

            var start0 = DateTime.Now;

            Populate(animeInfo);

            // save now for FK purposes
            var repAnime = new AniDB_AnimeRepository();
            repAnime.Save(session, this);

            var start = DateTime.Now;

            CreateEpisodes(session, eps);

            var ts = DateTime.Now - start;
            logger.Trace(string.Format("CreateEpisodes in : {0}", ts.TotalMilliseconds));
            start = DateTime.Now;

            CreateTitles(session, titles);
            ts = DateTime.Now - start;
            logger.Trace(string.Format("CreateTitles in : {0}", ts.TotalMilliseconds));
            start = DateTime.Now;

            CreateTags(session, tags);
            ts = DateTime.Now - start;
            logger.Trace(string.Format("CreateTags in : {0}", ts.TotalMilliseconds));
            start = DateTime.Now;

            CreateCharacters(session, chars);
            ts = DateTime.Now - start;
            logger.Trace(string.Format("CreateCharacters in : {0}", ts.TotalMilliseconds));
            start = DateTime.Now;

            CreateRelations(session, rels, downloadRelations);
            ts = DateTime.Now - start;
            logger.Trace(string.Format("CreateRelations in : {0}", ts.TotalMilliseconds));
            start = DateTime.Now;

            CreateSimilarAnime(session, sims);
            ts = DateTime.Now - start;
            logger.Trace(string.Format("CreateSimilarAnime in : {0}", ts.TotalMilliseconds));
            start = DateTime.Now;

            CreateRecommendations(session, recs);
            ts = DateTime.Now - start;
            logger.Trace(string.Format("CreateRecommendations in : {0}", ts.TotalMilliseconds));
            start = DateTime.Now;

            repAnime.Save(this);
            ts = DateTime.Now - start0;
            logger.Trace(string.Format("TOTAL TIME in : {0}", ts.TotalMilliseconds));
            logger.Trace("------------------------------------------------");
        }

        /// <summary>
        ///     we are depending on the HTTP api call to get most of the info
        ///     we only use UDP to get mssing information
        /// </summary>
        /// <param name="animeInfo"></param>
        public void PopulateAndSaveFromUDP(Raw_AniDB_Anime animeInfo)
        {
            // raw fields
            reviewIDListRAW = animeInfo.ReviewIDListRAW;

            // save now for FK purposes
            var repAnime = new AniDB_AnimeRepository();
            repAnime.Save(this);

            CreateAnimeReviews();
        }

        private void CreateEpisodes(ISession session, List<Raw_AniDB_Episode> eps)
        {
            if (eps == null) return;

            var repEps = new AniDB_EpisodeRepository();

            EpisodeCountSpecial = 0;
            EpisodeCountNormal = 0;

            var animeEpsToDelete = new List<AnimeEpisode>();
            var aniDBEpsToDelete = new List<AniDB_Episode>();

            foreach (var epraw in eps)
            {
                //List<AniDB_Episode> existingEps = repEps.GetByAnimeIDAndEpisodeTypeNumber(epraw.AnimeID, (enEpisodeType)epraw.EpisodeType, epraw.EpisodeNumber);
                // we need to do this check because some times AniDB will replace an existing episode with a new episode

                var tempEps = session
                    .CreateCriteria(typeof(AniDB_Episode))
                    .Add(Restrictions.Eq("AnimeID", epraw.AnimeID))
                    .Add(Restrictions.Eq("EpisodeNumber", epraw.EpisodeNumber))
                    .Add(Restrictions.Eq("EpisodeType", epraw.EpisodeType))
                    .List<AniDB_Episode>();

                var existingEps = new List<AniDB_Episode>(tempEps);

                // delete any old records
                foreach (var epOld in existingEps)
                {
                    if (epOld.EpisodeID != epraw.EpisodeID)
                    {
                        // first delete any AnimeEpisode records that point to the new anidb episode
                        var repAnimeEps = new AnimeEpisodeRepository();
                        var aniep = repAnimeEps.GetByAniDBEpisodeID(session, epOld.EpisodeID);
                        if (aniep != null)
                        {
                            //repAnimeEps.Delete(aniep.AnimeEpisodeID);
                            animeEpsToDelete.Add(aniep);
                        }

                        //repEps.Delete(epOld.AniDB_EpisodeID);
                        aniDBEpsToDelete.Add(epOld);
                    }
                }
            }
            using (var transaction = session.BeginTransaction())
            {
                foreach (var ep in animeEpsToDelete)
                    session.Delete(ep);

                transaction.Commit();
            }

            using (var transaction = session.BeginTransaction())
            {
                foreach (var ep in aniDBEpsToDelete)
                    session.Delete(ep);

                transaction.Commit();
            }


            var epsToSave = new List<AniDB_Episode>();
            foreach (var epraw in eps)
            {
                var epNew = session
                    .CreateCriteria(typeof(AniDB_Episode))
                    .Add(Restrictions.Eq("EpisodeID", epraw.EpisodeID))
                    .UniqueResult<AniDB_Episode>();


                if (epNew == null) epNew = new AniDB_Episode();

                epNew.Populate(epraw);
                epsToSave.Add(epNew);

                // since the HTTP api doesn't return a count of the number of specials, we will calculate it here
                if (epNew.EpisodeTypeEnum == enEpisodeType.Episode)
                {
                    EpisodeCountNormal++;
                }

                if (epNew.EpisodeTypeEnum == enEpisodeType.Special)
                    EpisodeCountSpecial++;
            }
            using (var transaction = session.BeginTransaction())
            {
                foreach (var rec in epsToSave)
                    session.SaveOrUpdate(rec);

                transaction.Commit();
            }


            EpisodeCount = EpisodeCountSpecial + EpisodeCountNormal;
        }

        private void CreateTitles(ISession session, List<Raw_AniDB_Anime_Title> titles)
        {
            if (titles == null) return;

            AllTitles = "";

            var titlesToDelete = new List<AniDB_Anime_Title>();
            var titlesToSave = new List<AniDB_Anime_Title>();

            var titlesTemp = session
                .CreateCriteria(typeof(AniDB_Anime_Title))
                .Add(Restrictions.Eq("AnimeID", AnimeID))
                .List<AniDB_Anime_Title>();

            titlesToDelete = new List<AniDB_Anime_Title>(titlesTemp);

            foreach (var rawtitle in titles)
            {
                var title = new AniDB_Anime_Title();
                title.Populate(rawtitle);
                titlesToSave.Add(title);

                if (AllTitles.Length > 0) AllTitles += "|";
                AllTitles += rawtitle.Title;
            }

            using (var transaction = session.BeginTransaction())
            {
                foreach (var tit in titlesToDelete)
                    session.Delete(tit);

                foreach (var tit in titlesToSave)
                    session.SaveOrUpdate(tit);

                transaction.Commit();
            }
        }

        private void CreateTags(ISession session, List<Raw_AniDB_Tag> tags)
        {
            if (tags == null) return;

            AllTags = "";

            var repTags = new AniDB_TagRepository();
            var repTagsXRefs = new AniDB_Anime_TagRepository();

            var tagsToSave = new List<AniDB_Tag>();
            var xrefsToSave = new List<AniDB_Anime_Tag>();
            var xrefsToDelete = new List<AniDB_Anime_Tag>();

            // find all the current links, and then later remove the ones that are no longer relevant
            var currentTags = repTagsXRefs.GetByAnimeID(AnimeID);
            var newTagIDs = new List<int>();

            foreach (var rawtag in tags)
            {
                var tag = repTags.GetByTagID(rawtag.TagID, session);
                if (tag == null) tag = new AniDB_Tag();

                tag.Populate(rawtag);
                tagsToSave.Add(tag);

                newTagIDs.Add(tag.TagID);

                var anime_tag = repTagsXRefs.GetByAnimeIDAndTagID(session, rawtag.AnimeID, rawtag.TagID);
                if (anime_tag == null) anime_tag = new AniDB_Anime_Tag();

                anime_tag.Populate(rawtag);
                xrefsToSave.Add(anime_tag);

                if (AllTags.Length > 0) AllTags += "|";
                AllTags += tag.TagName;
            }

            foreach (var curTag in currentTags)
            {
                if (!newTagIDs.Contains(curTag.TagID))
                    xrefsToDelete.Add(curTag);
            }

            using (var transaction = session.BeginTransaction())
            {
                foreach (var tag in tagsToSave)
                    session.SaveOrUpdate(tag);

                foreach (var xref in xrefsToSave)
                    session.SaveOrUpdate(xref);

                foreach (var xref in xrefsToDelete)
                    repTagsXRefs.Delete(xref.AniDB_Anime_TagID);

                transaction.Commit();
            }
        }

        private void CreateCharacters(ISession session, List<Raw_AniDB_Character> chars)
        {
            if (chars == null) return;

            var repChars = new AniDB_CharacterRepository();
            var repAnimeChars = new AniDB_Anime_CharacterRepository();
            var repCharSeiyuu = new AniDB_Character_SeiyuuRepository();
            var repSeiyuu = new AniDB_SeiyuuRepository();

            // delete all the existing cross references just in case one has been removed
            var animeChars = repAnimeChars.GetByAnimeID(session, AnimeID);

            using (var transaction = session.BeginTransaction())
            {
                foreach (var xref in animeChars)
                    session.Delete(xref);

                transaction.Commit();
            }


            var chrsToSave = new List<AniDB_Character>();
            var xrefsToSave = new List<AniDB_Anime_Character>();

            var seiyuuToSave = new Dictionary<int, AniDB_Seiyuu>();
            var seiyuuXrefToSave = new List<AniDB_Character_Seiyuu>();

            // delete existing relationships to seiyuu's
            var charSeiyuusToDelete = new List<AniDB_Character_Seiyuu>();
            foreach (var rawchar in chars)
            {
                // delete existing relationships to seiyuu's
                var allCharSei = repCharSeiyuu.GetByCharID(session, rawchar.CharID);
                foreach (var xref in allCharSei)
                    charSeiyuusToDelete.Add(xref);
            }
            using (var transaction = session.BeginTransaction())
            {
                foreach (var xref in charSeiyuusToDelete)
                    session.Delete(xref);

                transaction.Commit();
            }

            foreach (var rawchar in chars)
            {
                var chr = repChars.GetByCharID(session, rawchar.CharID);
                if (chr == null)
                    chr = new AniDB_Character();

                chr.PopulateFromHTTP(rawchar);
                chrsToSave.Add(chr);

                // create cross ref's between anime and character, but don't actually download anything
                var anime_char = new AniDB_Anime_Character();
                anime_char.Populate(rawchar);
                xrefsToSave.Add(anime_char);

                foreach (var rawSeiyuu in rawchar.Seiyuus)
                {
                    // save the link between character and seiyuu
                    var acc = repCharSeiyuu.GetByCharIDAndSeiyuuID(session, rawchar.CharID, rawSeiyuu.SeiyuuID);
                    if (acc == null)
                    {
                        acc = new AniDB_Character_Seiyuu();
                        acc.CharID = chr.CharID;
                        acc.SeiyuuID = rawSeiyuu.SeiyuuID;
                        seiyuuXrefToSave.Add(acc);
                    }

                    // save the seiyuu
                    var seiyuu = repSeiyuu.GetBySeiyuuID(session, rawSeiyuu.SeiyuuID);
                    if (seiyuu == null) seiyuu = new AniDB_Seiyuu();
                    seiyuu.PicName = rawSeiyuu.PicName;
                    seiyuu.SeiyuuID = rawSeiyuu.SeiyuuID;
                    seiyuu.SeiyuuName = rawSeiyuu.SeiyuuName;
                    seiyuuToSave[seiyuu.SeiyuuID] = seiyuu;
                }
            }

            using (var transaction = session.BeginTransaction())
            {
                foreach (var chr in chrsToSave)
                    session.SaveOrUpdate(chr);

                foreach (var xref in xrefsToSave)
                    session.SaveOrUpdate(xref);

                foreach (var seiyuu in seiyuuToSave.Values)
                    session.SaveOrUpdate(seiyuu);

                foreach (var xrefSeiyuu in seiyuuXrefToSave)
                    session.SaveOrUpdate(xrefSeiyuu);

                transaction.Commit();
            }
        }

        private void CreateRelations(ISession session, List<Raw_AniDB_RelatedAnime> rels, bool downloadRelations)
        {
            if (rels == null) return;

            var repRels = new AniDB_Anime_RelationRepository();

            var relsToSave = new List<AniDB_Anime_Relation>();
            var cmdsToSave = new List<CommandRequest_GetAnimeHTTP>();

            foreach (var rawrel in rels)
            {
                var anime_rel = repRels.GetByAnimeIDAndRelationID(session, rawrel.AnimeID, rawrel.RelatedAnimeID);
                if (anime_rel == null) anime_rel = new AniDB_Anime_Relation();

                anime_rel.Populate(rawrel);
                relsToSave.Add(anime_rel);

                if (downloadRelations && ServerSettings.AutoGroupSeries)
                {
                    logger.Info("Adding command to download related anime for {0} ({1}), related anime ID = {2}",
                        MainTitle, AnimeID, anime_rel.RelatedAnimeID);

                    // I have disable the downloading of relations here because of banning issues
                    // basically we will download immediate relations, but not relations of relations

                    //CommandRequest_GetAnimeHTTP cr_anime = new CommandRequest_GetAnimeHTTP(rawrel.RelatedAnimeID, false, downloadRelations);
                    var cr_anime = new CommandRequest_GetAnimeHTTP(anime_rel.RelatedAnimeID, false, false);
                    cmdsToSave.Add(cr_anime);
                }
            }

            using (var transaction = session.BeginTransaction())
            {
                foreach (var anime_rel in relsToSave)
                    session.SaveOrUpdate(anime_rel);

                transaction.Commit();
            }

            // this is not part of the session/transaction because it does other operations in the save
            foreach (var cmd in cmdsToSave)
                cmd.Save();
        }

        private void CreateSimilarAnime(ISession session, List<Raw_AniDB_SimilarAnime> sims)
        {
            if (sims == null) return;

            var repSim = new AniDB_Anime_SimilarRepository();

            var recsToSave = new List<AniDB_Anime_Similar>();

            foreach (var rawsim in sims)
            {
                var anime_sim = repSim.GetByAnimeIDAndSimilarID(session, rawsim.AnimeID, rawsim.SimilarAnimeID);
                if (anime_sim == null) anime_sim = new AniDB_Anime_Similar();

                anime_sim.Populate(rawsim);
                recsToSave.Add(anime_sim);
            }

            using (var transaction = session.BeginTransaction())
            {
                foreach (var rec in recsToSave)
                    session.SaveOrUpdate(rec);

                transaction.Commit();
            }
        }

        private void CreateRecommendations(ISession session, List<Raw_AniDB_Recommendation> recs)
        {
            if (recs == null) return;

            //AniDB_RecommendationRepository repRecs = new AniDB_RecommendationRepository();

            var recsToSave = new List<AniDB_Recommendation>();

            foreach (var rawRec in recs)
            {
                var rec = session
                    .CreateCriteria(typeof(AniDB_Recommendation))
                    .Add(Restrictions.Eq("AnimeID", rawRec.AnimeID))
                    .Add(Restrictions.Eq("UserID", rawRec.UserID))
                    .UniqueResult<AniDB_Recommendation>();

                if (rec == null)
                    rec = new AniDB_Recommendation();
                rec.Populate(rawRec);
                recsToSave.Add(rec);
            }

            using (var transaction = session.BeginTransaction())
            {
                foreach (var rec in recsToSave)
                    session.SaveOrUpdate(rec);

                transaction.Commit();
            }
        }

        private void CreateAnimeReviews()
        {
            if (reviewIDListRAW != null)
            //Only create relations if the origin of the data if from Raw (WebService/AniDB)
            {
                if (reviewIDListRAW.Trim().Length == 0)
                    return;

                //Delete old if changed
                var repReviews = new AniDB_Anime_ReviewRepository();
                var animeReviews = repReviews.GetByAnimeID(AnimeID);
                foreach (var xref in animeReviews)
                {
                    repReviews.Delete(xref.AniDB_Anime_ReviewID);
                }


                var revs = reviewIDListRAW.Split(',');
                foreach (var review in revs)
                {
                    if (review.Trim().Length > 0)
                    {
                        var rev = 0;
                        int.TryParse(review.Trim(), out rev);
                        if (rev != 0)
                        {
                            var csr = new AniDB_Anime_Review();
                            csr.AnimeID = AnimeID;
                            csr.ReviewID = rev;
                            repReviews.Save(csr);
                        }
                    }
                }
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("AnimeID: " + AnimeID);
            sb.Append(" | Main Title: " + MainTitle);
            sb.Append(" | EpisodeCount: " + EpisodeCount);
            sb.Append(" | AirDate: " + AirDate);
            sb.Append(" | Picname: " + Picname);
            sb.Append(" | Type: " + AnimeTypeRAW);
            return sb.ToString();
        }

        public Contract_AniDBAnime ToContract(bool getDefaultImages, List<AniDB_Anime_Title> titles)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return ToContract(session, getDefaultImages, titles);
            }
        }

        public Contract_AniDBAnime ToContract(ISession session, bool getDefaultImages, List<AniDB_Anime_Title> titles)
        {
            var contract = new Contract_AniDBAnime();

            contract.AirDate = AirDate;
            contract.AllCategories = AllCategories;
            contract.AllCinemaID = AllCinemaID;
            contract.AllTags = AllTags;
            contract.AllTitles = AllTitles;
            contract.AnimeID = AnimeID;
            contract.AnimeNfo = AnimeNfo;
            contract.AnimePlanetID = AnimePlanetID;
            contract.AnimeType = AnimeType;
            contract.ANNID = ANNID;
            contract.AvgReviewRating = AvgReviewRating;
            contract.AwardList = AwardList;
            contract.BeginYear = BeginYear;
            contract.Description = Description;
            contract.DateTimeDescUpdated = DateTimeDescUpdated;
            contract.DateTimeUpdated = DateTimeUpdated;
            contract.EndDate = EndDate;
            contract.EndYear = EndYear;
            contract.EpisodeCount = EpisodeCount;
            contract.EpisodeCountNormal = EpisodeCountNormal;
            contract.EpisodeCountSpecial = EpisodeCountSpecial;
            contract.ImageEnabled = ImageEnabled;
            contract.LatestEpisodeNumber = LatestEpisodeNumber;
            contract.LatestEpisodeAirDate = LatestEpisodeAirDate;
            contract.MainTitle = MainTitle;
            contract.Picname = Picname;
            contract.Rating = Rating;
            contract.Restricted = Restricted;
            contract.ReviewCount = ReviewCount;
            contract.TempRating = TempRating;
            contract.TempVoteCount = TempVoteCount;
            contract.URL = URL;
            contract.VoteCount = VoteCount;

            if (titles == null)
                contract.FormattedTitle = GetFormattedTitle(session);
            else
                contract.FormattedTitle = GetFormattedTitle(titles);

            contract.DisableExternalLinksFlag = DisableExternalLinksFlag;

            if (getDefaultImages)
            {
                var defFanart = GetDefaultFanart(session);
                if (defFanart != null) contract.DefaultImageFanart = defFanart.ToContract(session);

                var defPoster = GetDefaultPoster(session);
                if (defPoster != null) contract.DefaultImagePoster = defPoster.ToContract(session);

                var defBanner = GetDefaultWideBanner(session);
                if (defBanner != null) contract.DefaultImageWideBanner = defBanner.ToContract(session);
            }

            return contract;
        }

        public AnimeFull ToContractAzure()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return ToContractAzure(session);
            }
        }

        public AnimeFull ToContractAzure(ISession session)
        {
            var contract = new AnimeFull();
            contract.Detail = new AnimeDetail();
            contract.Characters = new List<AnimeCharacter>();
            contract.Comments = new List<AnimeComment>();

            contract.Detail.AllTags = TagsString;
            contract.Detail.AllCategories = TagsString;
            contract.Detail.AnimeID = AnimeID;
            contract.Detail.AnimeName = MainTitle;
            contract.Detail.AnimeType = AnimeTypeDescription;
            contract.Detail.Description = Description;
            contract.Detail.EndDateLong = Utils.GetAniDBDateAsSeconds(EndDate);
            contract.Detail.StartDateLong = Utils.GetAniDBDateAsSeconds(AirDate);
            contract.Detail.EpisodeCountNormal = EpisodeCountNormal;
            contract.Detail.EpisodeCountSpecial = EpisodeCountSpecial;
            contract.Detail.FanartURL = GetDefaultFanartOnlineURL(session);
            contract.Detail.OverallRating = AniDBRating;
            contract.Detail.PosterURL = string.Format(Constants.URLS.AniDB_Images, Picname);
            contract.Detail.TotalVotes = AniDBTotalVotes;

            var repAnimeChar = new AniDB_Anime_CharacterRepository();
            var repChar = new AniDB_CharacterRepository();

            var animeChars = repAnimeChar.GetByAnimeID(session, AnimeID);

            if (animeChars != null || animeChars.Count > 0)
            {
                // first get all the main characters
                foreach (
                    var animeChar in
                        animeChars.Where(
                            item =>
                                item.CharType.Equals("main character in", StringComparison.InvariantCultureIgnoreCase)))
                {
                    var chr = repChar.GetByCharID(session, animeChar.CharID);
                    if (chr != null)
                        contract.Characters.Add(chr.ToContractAzure(animeChar));
                }

                // now get the rest
                foreach (
                    var animeChar in
                        animeChars.Where(
                            item =>
                                !item.CharType.Equals("main character in", StringComparison.InvariantCultureIgnoreCase))
                    )
                {
                    var chr = repChar.GetByCharID(session, animeChar.CharID);
                    if (chr != null)
                        contract.Characters.Add(chr.ToContractAzure(animeChar));
                }
            }

            var repBA = new AniDB_RecommendationRepository();

            foreach (var rec in repBA.GetByAnimeID(session, AnimeID))
            {
                var comment = new AnimeComment();

                comment.UserID = rec.UserID;
                comment.UserName = "";

                // Comment details
                comment.CommentText = rec.RecommendationText;
                comment.IsSpoiler = false;
                comment.CommentDateLong = 0;

                comment.ImageURL = string.Empty;

                var recType = (AniDBRecommendationType)rec.RecommendationType;
                switch (recType)
                {
                    case AniDBRecommendationType.ForFans:
                        comment.CommentType = (int)WhatPeopleAreSayingType.AniDBForFans;
                        break;
                    case AniDBRecommendationType.MustSee:
                        comment.CommentType = (int)WhatPeopleAreSayingType.AniDBMustSee;
                        break;
                    case AniDBRecommendationType.Recommended:
                        comment.CommentType = (int)WhatPeopleAreSayingType.AniDBRecommendation;
                        break;
                }

                comment.Source = "AniDB";
                contract.Comments.Add(comment);
            }

            return contract;
        }

        public Contract_AniDBAnime ToContract()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return ToContract(session);
            }
        }

        public Contract_AniDBAnime ToContract(ISession session)
        {
            return ToContract(session, false, null);
        }

        public Contract_AniDB_AnimeDetailed ToContractDetailed(ISession session)
        {
            var repTitles = new AniDB_Anime_TitleRepository();
            var repTags = new AniDB_TagRepository();

            var contract = new Contract_AniDB_AnimeDetailed();

            contract.AnimeTitles = new List<Contract_AnimeTitle>();
            contract.Tags = new List<Contract_AnimeTag>();
            contract.CustomTags = new List<Contract_CustomTag>();
            contract.AniDBAnime = ToContract(session);

            // get all the anime titles
            var animeTitles = repTitles.GetByAnimeID(session, AnimeID);
            if (animeTitles != null)
            {
                foreach (var title in animeTitles)
                {
                    var ctitle = new Contract_AnimeTitle();
                    ctitle.AnimeID = title.AnimeID;
                    ctitle.Language = title.Language;
                    ctitle.Title = title.Title;
                    ctitle.TitleType = title.TitleType;
                    contract.AnimeTitles.Add(ctitle);
                }
            }


            var dictAnimeTags = new Dictionary<int, AniDB_Anime_Tag>();
            foreach (var animeTag in GetAnimeTags(session))
                dictAnimeTags[animeTag.TagID] = animeTag;

            foreach (var tag in GetAniDBTags(session))
            {
                var ctag = new Contract_AnimeTag();

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
            foreach (var custag in GetCustomTagsForAnime(session))
                contract.CustomTags.Add(custag.ToContract());

            if (UserVote != null)
                contract.UserVote = UserVote.ToContract();

            var repAdHoc = new AdhocRepository();
            var audioLanguages = new List<string>();
            var subtitleLanguages = new List<string>();

            //logger.Trace(" XXXX 06");

            // audio languages
            var dicAudio = repAdHoc.GetAudioLanguageStatsByAnime(session, AnimeID);
            foreach (var kvp in dicAudio)
            {
                foreach (var lanName in kvp.Value.LanguageNames)
                {
                    if (!audioLanguages.Contains(lanName))
                        audioLanguages.Add(lanName);
                }
            }

            //logger.Trace(" XXXX 07");

            // subtitle languages
            var dicSubtitle = repAdHoc.GetSubtitleLanguageStatsByAnime(session, AnimeID);
            foreach (var kvp in dicSubtitle)
            {
                foreach (var lanName in kvp.Value.LanguageNames)
                {
                    if (!subtitleLanguages.Contains(lanName))
                        subtitleLanguages.Add(lanName);
                }
            }

            //logger.Trace(" XXXX 08");

            contract.Stat_AudioLanguages = "";
            foreach (var audioLan in audioLanguages)
            {
                if (contract.Stat_AudioLanguages.Length > 0) contract.Stat_AudioLanguages += ",";
                contract.Stat_AudioLanguages += audioLan;
            }

            //logger.Trace(" XXXX 09");

            contract.Stat_SubtitleLanguages = "";
            foreach (var subLan in subtitleLanguages)
            {
                if (contract.Stat_SubtitleLanguages.Length > 0) contract.Stat_SubtitleLanguages += ",";
                contract.Stat_SubtitleLanguages += subLan;
            }

            //logger.Trace(" XXXX 10");
            contract.Stat_AllVideoQuality = repAdHoc.GetAllVideoQualityForAnime(session, AnimeID);

            contract.Stat_AllVideoQuality_Episodes = "";
            var stat = repAdHoc.GetEpisodeVideoQualityStatsForAnime(session, AnimeID);
            if (stat != null && stat.VideoQualityEpisodeCount.Count > 0)
            {
                foreach (var kvp in stat.VideoQualityEpisodeCount)
                {
                    if (kvp.Value >= EpisodeCountNormal)
                    {
                        if (contract.Stat_AllVideoQuality_Episodes.Length > 0)
                            contract.Stat_AllVideoQuality_Episodes += ",";
                        contract.Stat_AllVideoQuality_Episodes += kvp.Key;
                    }
                }
            }

            //logger.Trace(" XXXX 11");

            return contract;
        }


        public AnimeSeries CreateAnimeSeriesAndGroup()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return CreateAnimeSeriesAndGroup(session);
            }
        }

        public AnimeSeries CreateAnimeSeriesAndGroup(ISession session)
        {
            // create a new AnimeSeries record
            var repSeries = new AnimeSeriesRepository();
            var repGroups = new AnimeGroupRepository();

            var ser = new AnimeSeries();
            ser.Populate(this);

            var repUsers = new JMMUserRepository();
            var allUsers = repUsers.GetAll(session);

            // create the AnimeGroup record
            // check if there are any existing groups we could add this series to
            var createNewGroup = true;

            if (ServerSettings.AutoGroupSeries)
            {
                var grps = AnimeGroup.GetRelatedGroupsFromAnimeID(session, ser.AniDB_ID);

                // only use if there is just one result
                if (grps != null && grps.Count > 0)
                {
                    ser.AnimeGroupID = grps[0].AnimeGroupID;
                    createNewGroup = false;
                }
            }

            if (createNewGroup)
            {
                var anGroup = new AnimeGroup();
                anGroup.Populate(ser);
                repGroups.Save(anGroup);

                ser.AnimeGroupID = anGroup.AnimeGroupID;
            }

            repSeries.Save(ser);

            // check for TvDB associations
            var cmd = new CommandRequest_TvDBSearchAnime(AnimeID, false);
            cmd.Save();

            // check for Trakt associations
            if (ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
            {
                var cmd2 = new CommandRequest_TraktSearchAnime(AnimeID, false);
                cmd2.Save();
            }

            return ser;
        }

        public static void GetRelatedAnimeRecursive(ISession session, int animeID, ref List<AniDB_Anime> relList,
            ref List<int> relListIDs, ref List<int> searchedIDs)
        {
            var repAnime = new AniDB_AnimeRepository();
            var anime = repAnime.GetByAnimeID(animeID);
            searchedIDs.Add(animeID);

            foreach (var rel in anime.GetRelatedAnime(session))
            {
                var relationtype = rel.RelationType.ToLower();
                if ((relationtype == "same setting") || (relationtype == "alternative setting") ||
                    (relationtype == "character") || (relationtype == "other"))
                {
                    //Filter these relations these will fix messes, like Gundam , Clamp, etc.
                    continue;
                }
                var relAnime = repAnime.GetByAnimeID(session, rel.RelatedAnimeID);
                if (relAnime != null && !relListIDs.Contains(relAnime.AnimeID))
                {
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
        public string AllCategories { get; set; }
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
        public DateTime? LatestEpisodeAirDate { get; set; }
        public int DisableExternalLinksFlag { get; set; }

        #endregion
    }
}