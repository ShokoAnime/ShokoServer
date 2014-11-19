using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using System.Xml.Serialization;
using AniDBAPI;
using JMMServer.Repositories;
using JMMServer.Commands;
using JMMContracts;
using System.IO;
using JMMServer.ImageDownload;
using System.Diagnostics;
using NHibernate.Criterion;
using NHibernate;
using BinaryNorthwest;

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
		public int DisableExternalLinksFlag { get; set; }
		#endregion

		private static Logger logger = LogManager.GetCurrentClassLogger();

		// these files come from AniDB but we don't directly save them
		private string reviewIDListRAW;

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
					case enAnimeType.Movie: return "Movie";
					case enAnimeType.Other: return "Other";
					case enAnimeType.OVA: return "OVA";
					case enAnimeType.TVSeries: return "TV Series";
					case enAnimeType.TVSpecial: return "TV Special";
					case enAnimeType.Web: return "Web";
					default: return "Other";

				}
			}
		}

		public bool IsTvDBLinkDisabled
		{
			get
			{
				return (DisableExternalLinksFlag & Constants.FlagLinkTvDB) > 0;
			}
		}

		public bool IsTraktLinkDisabled
		{
			get
			{
				return (DisableExternalLinksFlag & Constants.FlagLinkTrakt) > 0;
			}
		}

		public bool IsMALLinkDisabled
		{
			get
			{
				return (DisableExternalLinksFlag & Constants.FlagLinkMAL) > 0;
			}
		}

		public bool IsMovieDBLinkDisabled
		{
			get
			{
				return (DisableExternalLinksFlag & Constants.FlagLinkMovieDB) > 0;
			}
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
				if (string.IsNullOrEmpty(Picname)) return "";

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
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetTvDBEpisodes(session);
			}
		}

		public List<TvDB_Episode> GetTvDBEpisodes(ISession session)
		{
			List<TvDB_Episode> tvDBEpisodes = new List<TvDB_Episode>();

			List<CrossRef_AniDB_TvDBV2> xrefs = GetCrossRefTvDBV2(session);
			if (xrefs.Count == 0) return tvDBEpisodes;

			TvDB_EpisodeRepository repEps = new TvDB_EpisodeRepository();
			foreach (CrossRef_AniDB_TvDBV2 xref in xrefs)
			{
				tvDBEpisodes.AddRange(repEps.GetBySeriesID(session, xref.TvDBID));
			}

			List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
			sortCriteria.Add(new SortPropOrFieldAndDirection("SeasonNumber", false, SortType.eInteger));
			sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeNumber", false, SortType.eInteger));
			tvDBEpisodes = Sorting.MultiSort<TvDB_Episode>(tvDBEpisodes, sortCriteria);

			return tvDBEpisodes;
		}

		private Dictionary<int, TvDB_Episode> dictTvDBEpisodes = null;

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
					logger.ErrorException(ex.ToString(), ex);
				}
			}
			return dictTvDBEpisodes;
		}

		private Dictionary<int, int> dictTvDBSeasons = null;

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
					logger.ErrorException(ex.ToString(), ex);
				}
			}
			return dictTvDBSeasons;
		}

		private Dictionary<int, int> dictTvDBSeasonsSpecials = null;

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
			CrossRef_AniDB_TvDB_EpisodeRepository repCrossRef = new CrossRef_AniDB_TvDB_EpisodeRepository();
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
			CrossRef_AniDB_TvDBV2Repository repCrossRef = new CrossRef_AniDB_TvDBV2Repository();
			return repCrossRef.GetByAnimeID(session, this.AnimeID);
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
            CrossRef_AniDB_TraktV2Repository repCrossRef = new CrossRef_AniDB_TraktV2Repository();
			return repCrossRef.GetByAnimeID(session, this.AnimeID);
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
			CrossRef_AniDB_MALRepository repCrossRef = new CrossRef_AniDB_MALRepository();
			return repCrossRef.GetByAnimeID(session, this.AnimeID);
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
			TvDB_SeriesRepository repSeries = new TvDB_SeriesRepository();

			List<TvDB_Series> ret = new List<TvDB_Series>();
			List<CrossRef_AniDB_TvDBV2> xrefs = GetCrossRefTvDBV2(session);
			if (xrefs.Count == 0) return ret;

			foreach (CrossRef_AniDB_TvDBV2 xref in xrefs)
			{
				TvDB_Series ser = repSeries.GetByTvDBID(session, xref.TvDBID);
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
			List<TvDB_ImageFanart> ret = new List<TvDB_ImageFanart>();

			List<CrossRef_AniDB_TvDBV2> xrefs = GetCrossRefTvDBV2(session);
			if (xrefs.Count == 0) return ret;

			TvDB_ImageFanartRepository repFanart = new TvDB_ImageFanartRepository();
			foreach (CrossRef_AniDB_TvDBV2 xref in xrefs)
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
			List<TvDB_ImagePoster> ret = new List<TvDB_ImagePoster>();

			List<CrossRef_AniDB_TvDBV2> xrefs = GetCrossRefTvDBV2(session);
			if (xrefs.Count == 0) return ret;

			TvDB_ImagePosterRepository repPosters = new TvDB_ImagePosterRepository();

			foreach (CrossRef_AniDB_TvDBV2 xref in xrefs)
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
			List<TvDB_ImageWideBanner> ret = new List<TvDB_ImageWideBanner>();

			List<CrossRef_AniDB_TvDBV2> xrefs = GetCrossRefTvDBV2(session);
			if (xrefs.Count == 0) return ret;

			TvDB_ImageWideBannerRepository repBanners = new TvDB_ImageWideBannerRepository();
			foreach (CrossRef_AniDB_TvDBV2 xref in xrefs)
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
			CrossRef_AniDB_OtherRepository repCrossRef = new CrossRef_AniDB_OtherRepository();
			return repCrossRef.GetByAnimeIDAndType(session, this.AnimeID, CrossRefType.MovieDB);
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
			CrossRef_AniDB_Other xref = GetCrossRefMovieDB(session);
			if (xref == null) return null;

			MovieDB_MovieRepository repMovies = new MovieDB_MovieRepository();
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
			CrossRef_AniDB_Other xref = GetCrossRefMovieDB(session);
			if (xref == null) return new List<MovieDB_Fanart>();

			MovieDB_FanartRepository repFanart = new MovieDB_FanartRepository();
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
			CrossRef_AniDB_Other xref = GetCrossRefMovieDB(session);
			if (xref == null) return new List<MovieDB_Poster>();

			MovieDB_PosterRepository repPosters = new MovieDB_PosterRepository();
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
			AniDB_Anime_DefaultImageRepository repDefaults = new AniDB_Anime_DefaultImageRepository();
			return repDefaults.GetByAnimeIDAndImagezSizeType(session, this.AnimeID, (int)ImageSizeType.Poster);
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
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetDefaultPosterPathNoBlanks(session);
			}
		}

		public string GetDefaultPosterPathNoBlanks(ISession session)
		{
			AniDB_Anime_DefaultImage defaultPoster = GetDefaultPoster(session);
			if (defaultPoster == null)
				return PosterPathNoDefault;
			else
			{
				ImageEntityType imageType = (ImageEntityType)defaultPoster.ImageParentType;

				switch (imageType)
				{
					case ImageEntityType.AniDB_Cover:
						return this.PosterPath;

					case ImageEntityType.TvDB_Cover:

						TvDB_ImagePosterRepository repTvPosters = new TvDB_ImagePosterRepository();
						TvDB_ImagePoster tvPoster = repTvPosters.GetByID(session, defaultPoster.ImageParentID);
						if (tvPoster != null)
							return tvPoster.FullImagePath;
						else
							return this.PosterPath;

					case ImageEntityType.Trakt_Poster:

						Trakt_ImagePosterRepository repTraktPosters = new Trakt_ImagePosterRepository();
						Trakt_ImagePoster traktPoster = repTraktPosters.GetByID(session, defaultPoster.ImageParentID);
						if (traktPoster != null)
							return traktPoster.FullImagePath;
						else
							return this.PosterPath;

					case ImageEntityType.MovieDB_Poster:

						MovieDB_PosterRepository repMoviePosters = new MovieDB_PosterRepository();
						MovieDB_Poster moviePoster = repMoviePosters.GetByID(session, defaultPoster.ImageParentID);
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
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetDefaultPosterDetailsNoBlanks(session);
			}
		}

		public ImageDetails GetDefaultPosterDetailsNoBlanks(ISession session)
		{
			ImageDetails details = new ImageDetails() { ImageType = JMMImageType.AniDB_Cover, ImageID = this.AnimeID };
			AniDB_Anime_DefaultImage defaultPoster = GetDefaultPoster(session);

			if (defaultPoster == null)
				return details;
			else
			{
				ImageEntityType imageType = (ImageEntityType)defaultPoster.ImageParentType;

				switch (imageType)
				{
					case ImageEntityType.AniDB_Cover:
						return details;

					case ImageEntityType.TvDB_Cover:

						TvDB_ImagePosterRepository repTvPosters = new TvDB_ImagePosterRepository();
						TvDB_ImagePoster tvPoster = repTvPosters.GetByID(session, defaultPoster.ImageParentID);
						if (tvPoster != null)
							details = new ImageDetails() { ImageType = JMMImageType.TvDB_Cover, ImageID = tvPoster.TvDB_ImagePosterID };

						return details;

					case ImageEntityType.Trakt_Poster:

						Trakt_ImagePosterRepository repTraktPosters = new Trakt_ImagePosterRepository();
						Trakt_ImagePoster traktPoster = repTraktPosters.GetByID(session, defaultPoster.ImageParentID);
						if (traktPoster != null)
							details = new ImageDetails() { ImageType = JMMImageType.Trakt_Poster, ImageID = traktPoster.Trakt_ImagePosterID };

						return details;

					case ImageEntityType.MovieDB_Poster:

						MovieDB_PosterRepository repMoviePosters = new MovieDB_PosterRepository();
						MovieDB_Poster moviePoster = repMoviePosters.GetByID(session, defaultPoster.ImageParentID);
						if (moviePoster != null)
							details = new ImageDetails() { ImageType = JMMImageType.MovieDB_Poster, ImageID = moviePoster.MovieDB_PosterID };

						return details;

				}
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
			AniDB_Anime_DefaultImageRepository repDefaults = new AniDB_Anime_DefaultImageRepository();
			return repDefaults.GetByAnimeIDAndImagezSizeType(session, this.AnimeID, (int)ImageSizeType.Fanart);
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
			Random fanartRandom = new Random();

			ImageDetails details = null;
			if (GetDefaultFanart() == null)
			{
				// get a random fanart (only tvdb)
				if (this.AnimeTypeEnum == enAnimeType.Movie)
				{
					List<MovieDB_Fanart> fanarts = GetMovieDBFanarts(session);
					if (fanarts.Count == 0) return null;

					MovieDB_Fanart movieFanart = fanarts[fanartRandom.Next(0, fanarts.Count)];
					details = new ImageDetails() { ImageType = JMMImageType.MovieDB_FanArt, ImageID = movieFanart.MovieDB_FanartID };
					return details;
				}
				else
				{
					List<TvDB_ImageFanart> fanarts = GetTvDBImageFanarts(session);
					if (fanarts.Count == 0) return null;

					TvDB_ImageFanart tvFanart = fanarts[fanartRandom.Next(0, fanarts.Count)];
					details = new ImageDetails() { ImageType = JMMImageType.TvDB_FanArt, ImageID = tvFanart.TvDB_ImageFanartID };
					return details;
				}

			}
			else
			{
				ImageEntityType imageType = (ImageEntityType)GetDefaultFanart().ImageParentType;

				switch (imageType)
				{

					case ImageEntityType.TvDB_FanArt:

						TvDB_ImageFanartRepository repTvFanarts = new TvDB_ImageFanartRepository();
						TvDB_ImageFanart tvFanart = repTvFanarts.GetByID(session, GetDefaultFanart(session).ImageParentID);
						if (tvFanart != null)
							details = new ImageDetails() { ImageType = JMMImageType.TvDB_FanArt, ImageID = tvFanart.TvDB_ImageFanartID };

						return details;

					case ImageEntityType.Trakt_Fanart:

						Trakt_ImageFanartRepository repTraktFanarts = new Trakt_ImageFanartRepository();
						Trakt_ImageFanart traktFanart = repTraktFanarts.GetByID(session, GetDefaultFanart(session).ImageParentID);
						if (traktFanart != null)
							details = new ImageDetails() { ImageType = JMMImageType.Trakt_Fanart, ImageID = traktFanart.Trakt_ImageFanartID };

						return details;

					case ImageEntityType.MovieDB_FanArt:

						MovieDB_FanartRepository repMovieFanarts = new MovieDB_FanartRepository();
						MovieDB_Fanart movieFanart = repMovieFanarts.GetByID(session, GetDefaultFanart(session).ImageParentID);
						if (movieFanart != null)
							details = new ImageDetails() { ImageType = JMMImageType.MovieDB_FanArt, ImageID = movieFanart.MovieDB_FanartID };

						return details;

				}
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
					return string.Format(Constants.URLS.TvDB_Images, tvFanart.BannerPath);
				}

			}
			else
			{
				ImageEntityType imageType = (ImageEntityType)GetDefaultFanart().ImageParentType;

				switch (imageType)
				{

					case ImageEntityType.TvDB_FanArt:

						TvDB_ImageFanartRepository repTvFanarts = new TvDB_ImageFanartRepository();
						TvDB_ImageFanart tvFanart = repTvFanarts.GetByID(GetDefaultFanart(session).ImageParentID);
						if (tvFanart != null)
							return string.Format(Constants.URLS.TvDB_Images, tvFanart.BannerPath);

						break;

					case ImageEntityType.Trakt_Fanart:

						Trakt_ImageFanartRepository repTraktFanarts = new Trakt_ImageFanartRepository();
						Trakt_ImageFanart traktFanart = repTraktFanarts.GetByID(GetDefaultFanart(session).ImageParentID);
						if (traktFanart != null)
							return traktFanart.ImageURL;

						break;

					case ImageEntityType.MovieDB_FanArt:

						MovieDB_FanartRepository repMovieFanarts = new MovieDB_FanartRepository();
						MovieDB_Fanart movieFanart = repMovieFanarts.GetByID(GetDefaultFanart(session).ImageParentID);
						if (movieFanart != null)
							return movieFanart.URL;

						break;

				}
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
			AniDB_Anime_DefaultImageRepository repDefaults = new AniDB_Anime_DefaultImageRepository();
			return repDefaults.GetByAnimeIDAndImagezSizeType(session, this.AnimeID, (int)ImageSizeType.WideBanner);
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
		public string CategoriesString
		{
			get
			{
				List<AniDB_Category> cats = GetCategories();
				string temp = "";
				foreach (AniDB_Category cr in cats)
					temp += cr.CategoryName + "|";
				if (temp.Length > 2)
					temp = temp.Substring(0, temp.Length - 2);
				return temp;
			}
		}


		[XmlIgnore]
		public bool SearchOnTvDB
		{
			get
			{
				return (AnimeType != (int)AnimeTypes.Movie);
			}
		}

		[XmlIgnore]
		public bool SearchOnMovieDB
		{
			get
			{
				return (AnimeType == (int)AnimeTypes.Movie);
			}
		}

		public List<AniDB_Category> GetCategories()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetCategories(session);
			}
		}

		public List<AniDB_Category> GetCategories(ISession session)
		{

			AniDB_CategoryRepository repCat = new AniDB_CategoryRepository();

			List<AniDB_Category> categories = new List<AniDB_Category>();
			foreach (AniDB_Anime_Category cat in GetAnimeCategories(session))
			{
				AniDB_Category newcat = repCat.GetByCategoryID(session, cat.CategoryID);
				if (newcat != null) categories.Add(newcat);
			}
			return categories;
		}

		/*[XmlIgnore]
		public List<AniDB_Anime_Category> AnimeCategories
		{
			get
			{
				AniDB_Anime_CategoryRepository repCatXRef = new AniDB_Anime_CategoryRepository();
				return repCatXRef.GetByAnimeID(AnimeID);
			}
		}*/

		public List<AniDB_Anime_Category> GetAnimeCategories()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetAnimeCategories(session);
			}
		}

		public List<AniDB_Anime_Category> GetAnimeCategories(ISession session)
		{
			AniDB_Anime_CategoryRepository repCatXRef = new AniDB_Anime_CategoryRepository();
			return repCatXRef.GetByAnimeID(session, AnimeID);
		}

		public List<AniDB_Category> GetAniDBCategories(ISession session)
		{
			AniDB_CategoryRepository repCats = new AniDB_CategoryRepository();
			return repCats.GetByAnimeID(session, AnimeID);
		}

        public List<CustomTag> GetCustomTagsForAnime(ISession session)
        {
            CustomTagRepository repTags = new CustomTagRepository();
            return repTags.GetByAnimeID(session, AnimeID);
        }

		public List<AniDB_Tag> GetAniDBTags(ISession session)
		{
			AniDB_TagRepository repTags = new AniDB_TagRepository();
			return repTags.GetByAnimeID(session, AnimeID);
		}

		public List<AniDB_Anime_Tag> GetAnimeTags(ISession session)
		{
			AniDB_Anime_TagRepository repAnimeTags = new AniDB_Anime_TagRepository();
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
			AniDB_Anime_RelationRepository repRels = new AniDB_Anime_RelationRepository();
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
			AniDB_Anime_SimilarRepository rep = new AniDB_Anime_SimilarRepository();
			return rep.GetByAnimeID(session, AnimeID);
		}

		[XmlIgnore]
		public List<AniDB_Anime_Review> AnimeReviews
		{
			get
			{
				AniDB_Anime_ReviewRepository RepRevs = new AniDB_Anime_ReviewRepository();
				return RepRevs.GetByAnimeID(AnimeID);
			}
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
			List<AniDB_Anime> relList = new List<AniDB_Anime>();
			List<int> relListIDs = new List<int>();
			List<int> searchedIDs = new List<int>();

			GetRelatedAnimeRecursive(session, this.AnimeID, ref relList, ref relListIDs, ref searchedIDs);
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
			AniDB_Anime_CharacterRepository repRels = new AniDB_Anime_CharacterRepository();
			return repRels.GetByAnimeID(session, AnimeID);
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
						return AniDBTotalRating / (decimal)AniDBTotalVotes;

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
					totalRating += ((decimal)Rating * VoteCount);
					totalRating += ((decimal)TempRating * TempVoteCount);

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
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetTitles(session);
			}
		}

		public List<AniDB_Anime_Title> GetTitles(ISession session)
		{
			AniDB_Anime_TitleRepository repTitles = new AniDB_Anime_TitleRepository();
			return repTitles.GetByAnimeID(session, AnimeID);
		}

		public string GetFormattedTitle(List<AniDB_Anime_Title> titles)
		{
			foreach (NamingLanguage nlan in Languages.PreferredNamingLanguages)
			{
				string thisLanguage = nlan.Language.Trim().ToUpper();
					
				// Romaji and English titles will be contained in MAIN and/or OFFICIAL
				// we won't use synonyms for these two languages
				if (thisLanguage.Equals(Constants.AniDBLanguageType.Romaji) || thisLanguage.Equals(Constants.AniDBLanguageType.English))
				{
					foreach (AniDB_Anime_Title title in titles)
					{
						string titleType = title.TitleType.Trim().ToUpper();
						// first try the  Main title
						if (titleType == Constants.AnimeTitleType.Main.ToUpper() && title.Language.Trim().ToUpper() == thisLanguage) return title.Title;
					}
				}

				// now try the official title
				foreach (AniDB_Anime_Title title in titles)
				{
					string titleType = title.TitleType.Trim().ToUpper();
					if (titleType == Constants.AnimeTitleType.Official.ToUpper() && title.Language.Trim().ToUpper() == thisLanguage) return title.Title;
				}

				// try synonyms
				if (ServerSettings.LanguageUseSynonyms)
				{
					foreach (AniDB_Anime_Title title in titles)
					{
						string titleType = title.TitleType.Trim().ToUpper();
						if (titleType == Constants.AnimeTitleType.Synonym.ToUpper() && title.Language.Trim().ToUpper() == thisLanguage) return title.Title;
					}
				}

			}

			// otherwise just use the main title
			return this.MainTitle;

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
			List<AniDB_Anime_Title> thisTitles = this.GetTitles(session);
			return GetFormattedTitle(thisTitles);
		}

		[XmlIgnore]
		public AniDB_Vote UserVote
		{
			get
			{
				try
				{
					AniDB_VoteRepository repVotes = new AniDB_VoteRepository();
					AniDB_Vote dbVote = repVotes.GetByAnimeID(this.AnimeID);
					return dbVote;
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
				AniDB_VoteRepository repVotes = new AniDB_VoteRepository();
				AniDB_Vote dbVote = repVotes.GetByAnimeID(session, this.AnimeID);
				return dbVote;
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
								titles[i].TitleType.Trim().ToUpper() == Constants.AnimeTitleType.Main.ToUpper()) return titles[i].Title;
						}
					}

					// now try the official title
					for (int i = 0; i < titles.Count; i++)
					{
						if (titles[i].Language.Trim().ToUpper() == thisLanguage &&
							titles[i].TitleType.Trim().ToUpper() == Constants.AnimeTitleType.Official.ToUpper()) return titles[i].Title;
					}

					// try synonyms
					if (ServerSettings.LanguageUseSynonyms)
					{
						for (int i = 0; i < titles.Count; i++)
						{
							if (titles[i].Language.Trim().ToUpper() == thisLanguage &&
								titles[i].TitleType.Trim().ToUpper() == Constants.AnimeTitleType.Synonym.ToUpper()) return titles[i].Title;
						}
					}

				}

				// otherwise just use the main title
				for (int i = 0; i < titles.Count; i++)
				{
					if (titles[i].TitleType.Trim().ToUpper() == Constants.AnimeTitleType.Main.ToUpper()) return titles[i].Title;
				}

				return "ERROR";
			}
		}


		[XmlIgnore]
		public List<AniDB_Episode> AniDBEpisodes
		{
			get
			{
				AniDB_EpisodeRepository repEps = new AniDB_EpisodeRepository();
				return repEps.GetByAnimeID(AnimeID);
			}
		}

		public List<AniDB_Episode> GetAniDBEpisodes(ISession session)
		{
			AniDB_EpisodeRepository repEps = new AniDB_EpisodeRepository();
			return repEps.GetByAnimeID(session, AnimeID);
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
			this.AllCategories = "";
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

		public void PopulateAndSaveFromHTTP(ISession session, Raw_AniDB_Anime animeInfo, List<Raw_AniDB_Episode> eps, List<Raw_AniDB_Anime_Title> titles,
			List<Raw_AniDB_Category> cats, List<Raw_AniDB_Tag> tags, List<Raw_AniDB_Character> chars, List<Raw_AniDB_RelatedAnime> rels, List<Raw_AniDB_SimilarAnime> sims,
			List<Raw_AniDB_Recommendation> recs, bool downloadRelations)
		{
			logger.Trace("------------------------------------------------");
			logger.Trace(string.Format("PopulateAndSaveFromHTTP: for {0} - {1}", animeInfo.AnimeID, animeInfo.MainTitle)); 
			logger.Trace("------------------------------------------------"); 

			DateTime start0 = DateTime.Now;

			Populate(animeInfo);

			// save now for FK purposes
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			repAnime.Save(session, this);

			DateTime start = DateTime.Now;

			CreateEpisodes(session, eps);
			TimeSpan ts = DateTime.Now - start; logger.Trace(string.Format("CreateEpisodes in : {0}", ts.TotalMilliseconds)); start = DateTime.Now;

			CreateTitles(session, titles);
			ts = DateTime.Now - start; logger.Trace(string.Format("CreateTitles in : {0}", ts.TotalMilliseconds)); start = DateTime.Now;

			CreateCategories(session, cats);
			ts = DateTime.Now - start; logger.Trace(string.Format("CreateCategories in : {0}", ts.TotalMilliseconds)); start = DateTime.Now;

			CreateTags(session, tags);
			ts = DateTime.Now - start; logger.Trace(string.Format("CreateTags in : {0}", ts.TotalMilliseconds)); start = DateTime.Now;

			CreateCharacters(session, chars);
			ts = DateTime.Now - start; logger.Trace(string.Format("CreateCharacters in : {0}", ts.TotalMilliseconds)); start = DateTime.Now;

			CreateRelations(session, rels, downloadRelations);
			ts = DateTime.Now - start; logger.Trace(string.Format("CreateRelations in : {0}", ts.TotalMilliseconds)); start = DateTime.Now;

			CreateSimilarAnime(session, sims);
			ts = DateTime.Now - start; logger.Trace(string.Format("CreateSimilarAnime in : {0}", ts.TotalMilliseconds)); start = DateTime.Now;

			CreateRecommendations(session, recs);
			ts = DateTime.Now - start; logger.Trace(string.Format("CreateRecommendations in : {0}", ts.TotalMilliseconds)); start = DateTime.Now;

			repAnime.Save(this);
			ts = DateTime.Now - start0; logger.Trace(string.Format("TOTAL TIME in : {0}", ts.TotalMilliseconds));
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
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			repAnime.Save(this);

			CreateAnimeReviews();
		}

		private void CreateEpisodes(ISession session, List<Raw_AniDB_Episode> eps)
		{
			if (eps == null) return;

			AniDB_EpisodeRepository repEps = new AniDB_EpisodeRepository();

			this.EpisodeCountSpecial = 0;
			this.EpisodeCountNormal = 0;

			List<AnimeEpisode> animeEpsToDelete = new List<AnimeEpisode>();
			List<AniDB_Episode> aniDBEpsToDelete = new List<AniDB_Episode>();

			foreach (Raw_AniDB_Episode epraw in eps)
			{
				//List<AniDB_Episode> existingEps = repEps.GetByAnimeIDAndEpisodeTypeNumber(epraw.AnimeID, (enEpisodeType)epraw.EpisodeType, epraw.EpisodeNumber);
				// we need to do this check because some times AniDB will replace an existing episode with a new episode

				var tempEps = session
					.CreateCriteria(typeof(AniDB_Episode))
					.Add(Restrictions.Eq("AnimeID", epraw.AnimeID))
					.Add(Restrictions.Eq("EpisodeNumber", epraw.EpisodeNumber))
					.Add(Restrictions.Eq("EpisodeType", epraw.EpisodeType))
					.List<AniDB_Episode>();

				List<AniDB_Episode> existingEps = new List<AniDB_Episode>(tempEps);

				// delete any old records
				foreach (AniDB_Episode epOld in existingEps)
				{
					if (epOld.EpisodeID != epraw.EpisodeID)
					{
						// first delete any AnimeEpisode records that point to the new anidb episode
						AnimeEpisodeRepository repAnimeEps = new AnimeEpisodeRepository();
						AnimeEpisode aniep = repAnimeEps.GetByAniDBEpisodeID(session, epOld.EpisodeID);
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
				foreach (AnimeEpisode ep in animeEpsToDelete)
					session.Delete(ep);

				transaction.Commit();
			}

			using (var transaction = session.BeginTransaction())
			{
				foreach (AniDB_Episode ep in aniDBEpsToDelete)
					session.Delete(ep);

				transaction.Commit();
			}




			List<AniDB_Episode> epsToSave = new List<AniDB_Episode>();
			foreach (Raw_AniDB_Episode epraw in eps)
			{
				AniDB_Episode epNew = session
					.CreateCriteria(typeof(AniDB_Episode))
					.Add(Restrictions.Eq("EpisodeID", epraw.EpisodeID))
					.UniqueResult<AniDB_Episode>();


				if (epNew == null) epNew = new AniDB_Episode();

				epNew.Populate(epraw);
				epsToSave.Add(epNew);

				// since the HTTP api doesn't return a count of the number of specials, we will calculate it here
				if (epNew.EpisodeTypeEnum == AniDBAPI.enEpisodeType.Episode)
					this.EpisodeCountNormal++;

				if (epNew.EpisodeTypeEnum == AniDBAPI.enEpisodeType.Special)
					this.EpisodeCountSpecial++;
			}
			using (var transaction = session.BeginTransaction())
			{
				foreach (AniDB_Episode rec in epsToSave)
					session.SaveOrUpdate(rec);

				transaction.Commit();
			}


			this.EpisodeCount = EpisodeCountSpecial + EpisodeCountNormal;
		}

		private void CreateTitles(ISession session, List<Raw_AniDB_Anime_Title> titles)
		{
			if (titles == null) return;

			this.AllTitles = "";

			List<AniDB_Anime_Title> titlesToDelete = new List<AniDB_Anime_Title>();
			List<AniDB_Anime_Title> titlesToSave = new List<AniDB_Anime_Title>();

			var titlesTemp = session
				.CreateCriteria(typeof(AniDB_Anime_Title))
				.Add(Restrictions.Eq("AnimeID", this.AnimeID))
				.List<AniDB_Anime_Title>();

			titlesToDelete = new List<AniDB_Anime_Title>(titlesTemp);

			foreach (Raw_AniDB_Anime_Title rawtitle in titles)
			{
				AniDB_Anime_Title title = new AniDB_Anime_Title();
				title.Populate(rawtitle);
				titlesToSave.Add(title);

				if (this.AllTitles.Length > 0) this.AllTitles += "|";
				this.AllTitles += rawtitle.Title;
			}

			using (var transaction = session.BeginTransaction())
			{
				foreach (AniDB_Anime_Title tit in titlesToDelete)
					session.Delete(tit);

				foreach (AniDB_Anime_Title tit in titlesToSave)
					session.SaveOrUpdate(tit);

				transaction.Commit();
			}
		}

		private void CreateCategories(ISession session, List<Raw_AniDB_Category> cats)
		{
			if (cats == null) return;

			this.AllCategories = "";

			AniDB_CategoryRepository repCats = new AniDB_CategoryRepository();
			AniDB_Anime_CategoryRepository repXRefs = new AniDB_Anime_CategoryRepository();

			int count = 0;

			List<AniDB_Category> catsToSave = new List<AniDB_Category>();
			List<AniDB_Anime_Category> xrefsToSave = new List<AniDB_Anime_Category>();

			foreach (Raw_AniDB_Category rawcat in cats)
			{
				count++;

				AniDB_Category cat = session
				.CreateCriteria(typeof(AniDB_Category))
				.Add(Restrictions.Eq("CategoryID", rawcat.CategoryID))
				.UniqueResult<AniDB_Category>();

				if (cat == null) cat = new AniDB_Category();

				cat.Populate(rawcat);
				catsToSave.Add(cat);

				AniDB_Anime_Category anime_cat = session
					.CreateCriteria(typeof(AniDB_Anime_Category))
					.Add(Restrictions.Eq("AnimeID", rawcat.AnimeID))
					.Add(Restrictions.Eq("CategoryID", rawcat.CategoryID))
					.UniqueResult<AniDB_Anime_Category>();

				if (anime_cat == null) anime_cat = new AniDB_Anime_Category();

				anime_cat.Populate(rawcat);
				xrefsToSave.Add(anime_cat);

				if (this.AllCategories.Length > 0) this.AllCategories += "|";
				this.AllCategories += cat.CategoryName;
			}

			using (var transaction = session.BeginTransaction())
			{
				foreach (AniDB_Category cat in catsToSave)
					session.SaveOrUpdate(cat);

				foreach (AniDB_Anime_Category xref in xrefsToSave)
					session.SaveOrUpdate(xref);

				transaction.Commit();
			}
		}

		private void CreateTags(ISession session, List<Raw_AniDB_Tag> tags)
		{
			if (tags == null) return;

			this.AllTags = "";

			AniDB_TagRepository repTags = new AniDB_TagRepository();
			AniDB_Anime_TagRepository repTagsXRefs = new AniDB_Anime_TagRepository();

			List<AniDB_Tag> tagsToSave = new List<AniDB_Tag>();
			List<AniDB_Anime_Tag> xrefsToSave = new List<AniDB_Anime_Tag>();

			foreach (Raw_AniDB_Tag rawtag in tags)
			{
				AniDB_Tag tag = repTags.GetByTagID(rawtag.TagID, session);
				if (tag == null) tag = new AniDB_Tag();

				tag.Populate(rawtag);
				tagsToSave.Add(tag);

				AniDB_Anime_Tag anime_tag = repTagsXRefs.GetByAnimeIDAndTagID(session, rawtag.AnimeID, rawtag.TagID);
				if (anime_tag == null) anime_tag = new AniDB_Anime_Tag();

				anime_tag.Populate(rawtag);
				xrefsToSave.Add(anime_tag);

				if (this.AllTags.Length > 0) this.AllTags += "|";
				this.AllTags += tag.TagName;
			}

			using (var transaction = session.BeginTransaction())
			{
				foreach (AniDB_Tag tag in tagsToSave)
					session.SaveOrUpdate(tag);

				foreach (AniDB_Anime_Tag xref in xrefsToSave)
					session.SaveOrUpdate(xref);

				transaction.Commit();
			}
		}

		private void CreateCharacters(ISession session, List<Raw_AniDB_Character> chars)
		{
			if (chars == null) return;

			AniDB_CharacterRepository repChars = new AniDB_CharacterRepository();
			AniDB_Anime_CharacterRepository repAnimeChars = new AniDB_Anime_CharacterRepository();
			AniDB_Character_SeiyuuRepository repCharSeiyuu = new AniDB_Character_SeiyuuRepository();
			AniDB_SeiyuuRepository repSeiyuu = new AniDB_SeiyuuRepository();

			// delete all the existing cross references just in case one has been removed
			List<AniDB_Anime_Character> animeChars = repAnimeChars.GetByAnimeID(session, AnimeID);

			using (var transaction = session.BeginTransaction())
			{
				foreach (AniDB_Anime_Character xref in animeChars)
					session.Delete(xref);

				transaction.Commit();
			}
				

			List<AniDB_Character> chrsToSave = new List<AniDB_Character>();
			List<AniDB_Anime_Character> xrefsToSave = new List<AniDB_Anime_Character>();

			Dictionary<int, AniDB_Seiyuu> seiyuuToSave = new Dictionary<int, AniDB_Seiyuu>();
			List<AniDB_Character_Seiyuu> seiyuuXrefToSave = new List<AniDB_Character_Seiyuu>();

			// delete existing relationships to seiyuu's
			List<AniDB_Character_Seiyuu> charSeiyuusToDelete = new List<AniDB_Character_Seiyuu>();
			foreach (Raw_AniDB_Character rawchar in chars)
			{
				// delete existing relationships to seiyuu's
				List<AniDB_Character_Seiyuu> allCharSei = repCharSeiyuu.GetByCharID(session, rawchar.CharID);
				foreach (AniDB_Character_Seiyuu xref in allCharSei)
					charSeiyuusToDelete.Add(xref);
			}
			using (var transaction = session.BeginTransaction())
			{
				foreach (AniDB_Character_Seiyuu xref in charSeiyuusToDelete)
					session.Delete(xref);

				transaction.Commit();
			}

			foreach (Raw_AniDB_Character rawchar in chars)
			{
				AniDB_Character chr = repChars.GetByCharID(session, rawchar.CharID);
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
					AniDB_Character_Seiyuu acc = repCharSeiyuu.GetByCharIDAndSeiyuuID(session, rawchar.CharID, rawSeiyuu.SeiyuuID);
					if (acc == null)
					{
						acc = new AniDB_Character_Seiyuu();
						acc.CharID = chr.CharID;
						acc.SeiyuuID = rawSeiyuu.SeiyuuID;
						seiyuuXrefToSave.Add(acc);
					}

					// save the seiyuu
					AniDB_Seiyuu seiyuu = repSeiyuu.GetBySeiyuuID(session, rawSeiyuu.SeiyuuID);
					if (seiyuu == null) seiyuu = new AniDB_Seiyuu();
					seiyuu.PicName = rawSeiyuu.PicName;
					seiyuu.SeiyuuID = rawSeiyuu.SeiyuuID;
					seiyuu.SeiyuuName = rawSeiyuu.SeiyuuName;
					seiyuuToSave[seiyuu.SeiyuuID] = seiyuu;
				}
			}

			using (var transaction = session.BeginTransaction())
			{
				foreach (AniDB_Character chr in chrsToSave)
					session.SaveOrUpdate(chr);

				foreach (AniDB_Anime_Character xref in xrefsToSave)
					session.SaveOrUpdate(xref);

				foreach (AniDB_Seiyuu seiyuu in seiyuuToSave.Values)
					session.SaveOrUpdate(seiyuu);

				foreach (AniDB_Character_Seiyuu xrefSeiyuu in seiyuuXrefToSave)
					session.SaveOrUpdate(xrefSeiyuu);

				transaction.Commit();
			}

			
		}

		private void CreateRelations(ISession session, List<Raw_AniDB_RelatedAnime> rels, bool downloadRelations)
		{
			if (rels == null) return;

			AniDB_Anime_RelationRepository repRels = new AniDB_Anime_RelationRepository();

			List<AniDB_Anime_Relation> relsToSave = new List<AniDB_Anime_Relation>();
			List<CommandRequest_GetAnimeHTTP> cmdsToSave = new List<CommandRequest_GetAnimeHTTP>();

			foreach (Raw_AniDB_RelatedAnime rawrel in rels)
			{
				AniDB_Anime_Relation anime_rel = repRels.GetByAnimeIDAndRelationID(session, rawrel.AnimeID, rawrel.RelatedAnimeID);
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
					CommandRequest_GetAnimeHTTP cr_anime = new CommandRequest_GetAnimeHTTP(anime_rel.RelatedAnimeID, false, false);
					cmdsToSave.Add(cr_anime);
				}
			}

			using (var transaction = session.BeginTransaction())
			{
				foreach (AniDB_Anime_Relation anime_rel in relsToSave)
					session.SaveOrUpdate(anime_rel);

				transaction.Commit();
			}

			// this is not part of the session/transaction because it does other operations in the save
			foreach (CommandRequest_GetAnimeHTTP cmd in cmdsToSave)
				cmd.Save();
		}

		private void CreateSimilarAnime(ISession session, List<Raw_AniDB_SimilarAnime> sims)
		{
			if (sims == null) return;

			AniDB_Anime_SimilarRepository repSim = new AniDB_Anime_SimilarRepository();

			List<AniDB_Anime_Similar> recsToSave = new List<AniDB_Anime_Similar>();

			foreach (Raw_AniDB_SimilarAnime rawsim in sims)
			{
				AniDB_Anime_Similar anime_sim = repSim.GetByAnimeIDAndSimilarID(session, rawsim.AnimeID, rawsim.SimilarAnimeID);
				if (anime_sim == null) anime_sim = new AniDB_Anime_Similar();

				anime_sim.Populate(rawsim);
				recsToSave.Add(anime_sim);
			}

			using (var transaction = session.BeginTransaction())
			{
				foreach (AniDB_Anime_Similar rec in recsToSave)
					session.SaveOrUpdate(rec);

				transaction.Commit();
			}
		}

		private void CreateRecommendations(ISession session, List<Raw_AniDB_Recommendation> recs)
		{
			if (recs == null) return;

			//AniDB_RecommendationRepository repRecs = new AniDB_RecommendationRepository();

			List<AniDB_Recommendation> recsToSave = new List<AniDB_Recommendation>();

			foreach (Raw_AniDB_Recommendation rawRec in recs)
			{
				AniDB_Recommendation rec = session
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
				foreach (AniDB_Recommendation rec in recsToSave)
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
				AniDB_Anime_ReviewRepository repReviews = new AniDB_Anime_ReviewRepository();
				List<AniDB_Anime_Review> animeReviews = repReviews.GetByAnimeID(AnimeID);
				foreach (AniDB_Anime_Review xref in animeReviews)
				{
					repReviews.Delete(xref.AniDB_Anime_ReviewID);
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
							repReviews.Save(csr);
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

		public Contract_AniDBAnime ToContract(bool getDefaultImages, List<AniDB_Anime_Title> titles)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return ToContract(session, getDefaultImages, titles);
			}
		}

		public Contract_AniDBAnime ToContract(ISession session, bool getDefaultImages, List<AniDB_Anime_Title> titles)
		{
			Contract_AniDBAnime contract = new Contract_AniDBAnime();

			contract.AirDate = this.AirDate;
			contract.AllCategories = this.AllCategories;
			contract.AllCinemaID = this.AllCinemaID;
			contract.AllTags = this.AllTags;
			contract.AllTitles = this.AllTitles;
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

			if (titles == null)
				contract.FormattedTitle = this.GetFormattedTitle(session);
			else
				contract.FormattedTitle = GetFormattedTitle(titles);

			contract.DisableExternalLinksFlag = this.DisableExternalLinksFlag;

			if (getDefaultImages)
			{
				AniDB_Anime_DefaultImage defFanart = this.GetDefaultFanart(session);
				if (defFanart != null) contract.DefaultImageFanart = defFanart.ToContract(session);

				AniDB_Anime_DefaultImage defPoster = this.GetDefaultPoster(session);
				if (defPoster != null) contract.DefaultImagePoster = defPoster.ToContract(session);

				AniDB_Anime_DefaultImage defBanner = this.GetDefaultWideBanner(session);
				if (defBanner != null) contract.DefaultImageWideBanner = defBanner.ToContract(session);
			}

			return contract;
		}

		public JMMServer.Providers.Azure.AnimeFull ToContractAzure()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return ToContractAzure(session);
			}
		}

		public JMMServer.Providers.Azure.AnimeFull ToContractAzure(ISession session)
		{
			JMMServer.Providers.Azure.AnimeFull contract = new JMMServer.Providers.Azure.AnimeFull();
			contract.Detail = new Providers.Azure.AnimeDetail();
			contract.Characters = new List<Providers.Azure.AnimeCharacter>();
			contract.Shouts = new List<Providers.Azure.AnimeShout>();

			contract.Detail.AllCategories = this.CategoriesString;
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
			contract.Detail.PosterURL = string.Format(Constants.URLS.AniDB_Images, Picname);
			contract.Detail.TotalVotes = this.AniDBTotalVotes;

			AniDB_Anime_CharacterRepository repAnimeChar = new AniDB_Anime_CharacterRepository();
			AniDB_CharacterRepository repChar = new AniDB_CharacterRepository();

			List<AniDB_Anime_Character> animeChars = repAnimeChar.GetByAnimeID(session, AnimeID);

			if (animeChars != null || animeChars.Count > 0)
			{
				// first get all the main characters
				foreach (AniDB_Anime_Character animeChar in animeChars.Where(item => item.CharType.Equals("main character in", StringComparison.InvariantCultureIgnoreCase)))
				{
					AniDB_Character chr = repChar.GetByCharID(session, animeChar.CharID);
					if (chr != null)
						contract.Characters.Add(chr.ToContractAzure(animeChar));
				}

				// now get the rest
				foreach (AniDB_Anime_Character animeChar in animeChars.Where(item => !item.CharType.Equals("main character in", StringComparison.InvariantCultureIgnoreCase)))
				{
					AniDB_Character chr = repChar.GetByCharID(session, animeChar.CharID);
					if (chr != null)
						contract.Characters.Add(chr.ToContractAzure(animeChar));

				}
			}

			AniDB_RecommendationRepository repBA = new AniDB_RecommendationRepository();

			foreach (AniDB_Recommendation rec in repBA.GetByAnimeID(session, AnimeID))
			{
				JMMServer.Providers.Azure.AnimeShout shout = new JMMServer.Providers.Azure.AnimeShout();

				shout.UserID = rec.UserID;
				shout.UserName = "";

				// shout details
				shout.ShoutText = rec.RecommendationText;
				shout.IsSpoiler = false;
				shout.ShoutDateLong = 0;

				shout.ImageURL = string.Empty;

				AniDBRecommendationType recType = (AniDBRecommendationType)rec.RecommendationType;
				switch (recType)
				{
					case AniDBRecommendationType.ForFans: shout.ShoutType = (int)WhatPeopleAreSayingType.AniDBForFans; break;
					case AniDBRecommendationType.MustSee: shout.ShoutType = (int)WhatPeopleAreSayingType.AniDBMustSee; break;
					case AniDBRecommendationType.Recommended: shout.ShoutType = (int)WhatPeopleAreSayingType.AniDBRecommendation; break;
				}

				shout.Source = "AniDB";
				contract.Shouts.Add(shout);
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
			//logger.Trace(" XXXX 01");
			AniDB_Anime_TitleRepository repTitles = new AniDB_Anime_TitleRepository();
			AniDB_CategoryRepository repCats = new AniDB_CategoryRepository();
			AniDB_TagRepository repTags = new AniDB_TagRepository();

			Contract_AniDB_AnimeDetailed contract = new Contract_AniDB_AnimeDetailed();

			contract.AnimeTitles = new List<Contract_AnimeTitle>();
			contract.Categories = new List<Contract_AnimeCategory>();
			contract.Tags = new List<Contract_AnimeTag>();
            contract.CustomTags = new List<Contract_CustomTag>();
			contract.AniDBAnime = this.ToContract(session);

			//logger.Trace(" XXXX 02");

			// get all the anime titles
			List<AniDB_Anime_Title> animeTitles = repTitles.GetByAnimeID(session, AnimeID);
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

			//logger.Trace(" XXXX 03");

			Dictionary<int, AniDB_Anime_Category> dictAnimeCats = new Dictionary<int, AniDB_Anime_Category>();
			foreach (AniDB_Anime_Category animeCat in GetAnimeCategories(session))
				dictAnimeCats[animeCat.CategoryID] = animeCat;

			foreach (AniDB_Category cat in GetAniDBCategories(session))
			{
				Contract_AnimeCategory ccat = new Contract_AnimeCategory();
				ccat.CategoryDescription = cat.CategoryDescription;
				ccat.CategoryID = cat.CategoryID;
				ccat.CategoryName = cat.CategoryName;
				ccat.IsHentai = cat.IsHentai;
				ccat.ParentID = cat.ParentID;

				if (dictAnimeCats.ContainsKey(cat.CategoryID))
					ccat.Weighting = dictAnimeCats[cat.CategoryID].Weighting;
				else
					ccat.Weighting = 0;
				contract.Categories.Add(ccat);
			}

			//logger.Trace(" XXXX 04");

			Dictionary<int, AniDB_Anime_Tag> dictAnimeTags = new Dictionary<int, AniDB_Anime_Tag>();
			foreach (AniDB_Anime_Tag animeTag in GetAnimeTags(session))
				dictAnimeTags[animeTag.TagID] = animeTag;

			foreach (AniDB_Tag tag in GetAniDBTags(session))
			{
				Contract_AnimeTag ctag = new Contract_AnimeTag();
				
				ctag.GlobalSpoiler = tag.GlobalSpoiler;
				ctag.LocalSpoiler = tag.LocalSpoiler;
				ctag.Spoiler = tag.Spoiler;
				ctag.TagCount = tag.TagCount;
				ctag.TagDescription = tag.TagDescription;
				ctag.TagID = tag.TagID;
				ctag.TagName = tag.TagName;

				if (dictAnimeTags.ContainsKey(tag.TagID))
					ctag.Approval = dictAnimeTags[tag.TagID].Approval;
				else
					ctag.Approval = 0;
				contract.Tags.Add(ctag);
			}


            // Get all the custom tags
            foreach (CustomTag custag in GetCustomTagsForAnime(session))
                contract.CustomTags.Add(custag.ToContract());

			if (this.UserVote != null)
				contract.UserVote = this.UserVote.ToContract();

			AdhocRepository repAdHoc = new AdhocRepository();
			List<string> audioLanguages = new List<string>();
			List<string> subtitleLanguages = new List<string>();

			//logger.Trace(" XXXX 06");

			// audio languages
			Dictionary<int, LanguageStat> dicAudio = repAdHoc.GetAudioLanguageStatsByAnime(session, this.AnimeID);
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
			Dictionary<int, LanguageStat> dicSubtitle = repAdHoc.GetSubtitleLanguageStatsByAnime(session, this.AnimeID);
			foreach (KeyValuePair<int, LanguageStat> kvp in dicSubtitle)
			{
				foreach (string lanName in kvp.Value.LanguageNames)
				{
					if (!subtitleLanguages.Contains(lanName))
						subtitleLanguages.Add(lanName);
				}
			}

			//logger.Trace(" XXXX 08");

			contract.Stat_AudioLanguages = "";
			foreach (string audioLan in audioLanguages)
			{
				if (contract.Stat_AudioLanguages.Length > 0) contract.Stat_AudioLanguages += ",";
				contract.Stat_AudioLanguages += audioLan;
			}

			//logger.Trace(" XXXX 09");

			contract.Stat_SubtitleLanguages = "";
			foreach (string subLan in subtitleLanguages)
			{
				if (contract.Stat_SubtitleLanguages.Length > 0) contract.Stat_SubtitleLanguages += ",";
				contract.Stat_SubtitleLanguages += subLan;
			}

			//logger.Trace(" XXXX 10");
			contract.Stat_AllVideoQuality = repAdHoc.GetAllVideoQualityForAnime(session, this.AnimeID);

			contract.Stat_AllVideoQuality_Episodes = "";
			AnimeVideoQualityStat stat = repAdHoc.GetEpisodeVideoQualityStatsForAnime(session, this.AnimeID);
			if (stat != null && stat.VideoQualityEpisodeCount.Count > 0)
			{
				foreach (KeyValuePair<string, int> kvp in stat.VideoQualityEpisodeCount)
				{
					if (kvp.Value >= EpisodeCountNormal)
					{
						if (contract.Stat_AllVideoQuality_Episodes.Length > 0) contract.Stat_AllVideoQuality_Episodes += ",";
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
			AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
			AnimeGroupRepository repGroups = new AnimeGroupRepository();

			AnimeSeries ser = new AnimeSeries();
			ser.Populate(this);

			JMMUserRepository repUsers = new JMMUserRepository();
			List<JMMUser> allUsers = repUsers.GetAll(session);

			// create the AnimeGroup record
			// check if there are any existing groups we could add this series to
			bool createNewGroup = true;

			if (ServerSettings.AutoGroupSeries)
			{
				List<AnimeGroup> grps = AnimeGroup.GetRelatedGroupsFromAnimeID(session, ser.AniDB_ID);

				// only use if there is just one result
				if (grps != null && grps.Count > 0)
				{
					ser.AnimeGroupID = grps[0].AnimeGroupID;
					createNewGroup = false;
				}
			}

			if (createNewGroup)
			{
				AnimeGroup anGroup = new AnimeGroup();
				anGroup.Populate(ser);
				repGroups.Save(anGroup);

				ser.AnimeGroupID = anGroup.AnimeGroupID;
			}

			repSeries.Save(ser);

			// check for TvDB associations
			CommandRequest_TvDBSearchAnime cmd = new CommandRequest_TvDBSearchAnime(this.AnimeID, false);
			cmd.Save();

			// check for Trakt associations
            if (ServerSettings.WebCache_Trakt_Get)
            {
                CommandRequest_TraktSearchAnime cmd2 = new CommandRequest_TraktSearchAnime(this.AnimeID, false);
                cmd2.Save();
            }

			return ser;
		}

		public static void GetRelatedAnimeRecursive(ISession session, int animeID, ref List<AniDB_Anime> relList, ref List<int> relListIDs, ref List<int> searchedIDs)
		{
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			AniDB_Anime anime = repAnime.GetByAnimeID(animeID);
			searchedIDs.Add(animeID);

			foreach (AniDB_Anime_Relation rel in anime.GetRelatedAnime(session))
			{
                string relationtype = rel.RelationType.ToLower();
                if ((relationtype == "same setting") || (relationtype == "alternative setting") ||
                    (relationtype == "character") || (relationtype == "other"))
                {
                    //Filter these relations these will fix messes, like Gundam , Clamp, etc.
                    continue;
                }
				AniDB_Anime relAnime = repAnime.GetByAnimeID(session, rel.RelatedAnimeID);
				if (relAnime!= null && !relListIDs.Contains(relAnime.AnimeID))
				{
					relList.Add(relAnime);
					relListIDs.Add(relAnime.AnimeID);
					if (!searchedIDs.Contains(rel.RelatedAnimeID))
					{
						GetRelatedAnimeRecursive(session, rel.RelatedAnimeID, ref relList, ref relListIDs, ref searchedIDs);
					}
				}
			}
		}
	}
}
