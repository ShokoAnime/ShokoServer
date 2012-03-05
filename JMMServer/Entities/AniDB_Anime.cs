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

		public CrossRef_AniDB_TvDB CrossRefTvDB
		{
			get
			{
				CrossRef_AniDB_TvDBRepository repCrossRef = new CrossRef_AniDB_TvDBRepository();
				return repCrossRef.GetByAnimeID(this.AnimeID);
			}
		}

		public CrossRef_AniDB_Trakt CrossRefTrakt
		{
			get
			{
				CrossRef_AniDB_TraktRepository repCrossRef = new CrossRef_AniDB_TraktRepository();
				return repCrossRef.GetByAnimeID(this.AnimeID);
			}
		}

		public List<CrossRef_AniDB_MAL> CrossRefMAL
		{
			get
			{
				CrossRef_AniDB_MALRepository repCrossRef = new CrossRef_AniDB_MALRepository();
				return repCrossRef.GetByAnimeID(this.AnimeID);
			}
		}

		public Trakt_Show TraktShow
		{
			get
			{
				CrossRef_AniDB_Trakt xref = CrossRefTrakt;
				if (xref == null) return null;

				Trakt_ShowRepository repShows = new Trakt_ShowRepository();
				return repShows.GetByTraktID(xref.TraktID);
			}
		}

		public Trakt_ImagePoster TraktImagePoster
		{
			get
			{
				Trakt_Show show = TraktShow;
				if (show == null) return null;

				CrossRef_AniDB_Trakt xref = CrossRefTrakt;
				if (xref == null) return null;

				Trakt_ImagePosterRepository repPosters = new Trakt_ImagePosterRepository();
				return repPosters.GetByShowIDAndSeason(show.Trakt_ShowID, xref.TraktSeasonNumber);
			}
		}

		public Trakt_ImageFanart TraktImageFanart
		{
			get
			{
				Trakt_Show show = TraktShow;
				if (show == null) return null;

				Trakt_ImageFanartRepository repFanart = new Trakt_ImageFanartRepository();
				return repFanart.GetByShowIDAndSeason(show.Trakt_ShowID, 1);
			}
		}

		public TvDB_Series TvDBSeries
		{
			get
			{
				CrossRef_AniDB_TvDB xref = CrossRefTvDB;
				if (xref == null) return null;

				TvDB_SeriesRepository repSeries = new TvDB_SeriesRepository();
				return repSeries.GetByTvDBID(xref.TvDBID);
			}
		}

		public List<TvDB_Episode> TvDBEpisodes
		{
			get
			{
				CrossRef_AniDB_TvDB xref = CrossRefTvDB;
				if (xref == null) return new List<TvDB_Episode>();

				TvDB_EpisodeRepository repEps = new TvDB_EpisodeRepository();
				return repEps.GetBySeriesID(xref.TvDBID);
			}
		}

		public List<TvDB_ImageFanart> TvDBImageFanarts
		{
			get
			{
				CrossRef_AniDB_TvDB xref = CrossRefTvDB;
				if (xref == null) return new List<TvDB_ImageFanart>();

				TvDB_ImageFanartRepository repFanart = new TvDB_ImageFanartRepository();
				return repFanart.GetBySeriesID(xref.TvDBID);
			}
		}

		public List<TvDB_ImagePoster> TvDBImagePosters
		{
			get
			{
				CrossRef_AniDB_TvDB xref = CrossRefTvDB;
				if (xref == null) return new List<TvDB_ImagePoster>();

				TvDB_ImagePosterRepository repPosters = new TvDB_ImagePosterRepository();
				return repPosters.GetBySeriesID(xref.TvDBID);
			}
		}

		public List<TvDB_ImageWideBanner> TvDBImageWideBanners
		{
			get
			{
				CrossRef_AniDB_TvDB xref = CrossRefTvDB;
				if (xref == null) return new List<TvDB_ImageWideBanner>();

				TvDB_ImageWideBannerRepository repBanners = new TvDB_ImageWideBannerRepository();
				return repBanners.GetBySeriesID(xref.TvDBID);
			}
		}

		public CrossRef_AniDB_Other CrossRefMovieDB
		{
			get
			{
				CrossRef_AniDB_OtherRepository repCrossRef = new CrossRef_AniDB_OtherRepository();
				return repCrossRef.GetByAnimeIDAndType(this.AnimeID, CrossRefType.MovieDB);
			}
		}

		public MovieDB_Movie MovieDBMovie
		{
			get
			{
				CrossRef_AniDB_Other xref = CrossRefMovieDB;
				if (xref == null) return null;

				MovieDB_MovieRepository repMovies = new MovieDB_MovieRepository();
				return repMovies.GetByOnlineID(int.Parse(xref.CrossRefID));
			}
		}

		public List<MovieDB_Fanart> MovieDBFanarts
		{
			get
			{
				CrossRef_AniDB_Other xref = CrossRefMovieDB;
				if (xref == null) return new List<MovieDB_Fanart>();

				MovieDB_FanartRepository repFanart = new MovieDB_FanartRepository();
				return repFanart.GetByMovieID(int.Parse(xref.CrossRefID));
			}
		}

		public List<MovieDB_Poster> MovieDBPosters
		{
			get
			{
				CrossRef_AniDB_Other xref = CrossRefMovieDB;
				if (xref == null) return new List<MovieDB_Poster>();

				MovieDB_PosterRepository repPosters = new MovieDB_PosterRepository();
				return repPosters.GetByMovieID(int.Parse(xref.CrossRefID));
			}
		}

		public AniDB_Anime_DefaultImage DefaultPoster
		{
			get
			{
				AniDB_Anime_DefaultImageRepository repDefaults = new AniDB_Anime_DefaultImageRepository();
				return repDefaults.GetByAnimeIDAndImagezSizeType(this.AnimeID, (int)ImageSizeType.Poster);
			}
		}

		public AniDB_Anime_DefaultImage DefaultFanart
		{
			get
			{
				AniDB_Anime_DefaultImageRepository repDefaults = new AniDB_Anime_DefaultImageRepository();
				return repDefaults.GetByAnimeIDAndImagezSizeType(this.AnimeID, (int)ImageSizeType.Fanart);
			}
		}

		public AniDB_Anime_DefaultImage DefaultWideBanner
		{
			get
			{
				AniDB_Anime_DefaultImageRepository repDefaults = new AniDB_Anime_DefaultImageRepository();
				return repDefaults.GetByAnimeIDAndImagezSizeType(this.AnimeID, (int)ImageSizeType.WideBanner);
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
		public string CategoriesString
		{
			get
			{
				List<AniDB_Category> cats = Categories;
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

		[XmlIgnore]
		public List<AniDB_Category> Categories
		{
			get
			{
				AniDB_CategoryRepository repCat = new AniDB_CategoryRepository();

				List<AniDB_Category> categories = new List<AniDB_Category>();
				foreach (AniDB_Anime_Category cat in AnimeCategories)
				{
					AniDB_Category newcat = repCat.GetByID(cat.CategoryID);
					if (newcat != null) categories.Add(newcat);
				}
				categories.Sort();
				return categories;
			}
		}

		[XmlIgnore]
		public List<AniDB_Anime_Category> AnimeCategories
		{
			get
			{
				AniDB_Anime_CategoryRepository repCatXRef = new AniDB_Anime_CategoryRepository();
				return repCatXRef.GetByAnimeID(AnimeID);
			}
		}

		[XmlIgnore]
		public List<AniDB_Tag> Tags
		{
			get
			{
				AniDB_TagRepository repCat = new AniDB_TagRepository();

				List<AniDB_Tag> tags = new List<AniDB_Tag>();
				foreach (AniDB_Anime_Tag tag in AnimeTags)
				{
					AniDB_Tag newtag = repCat.GetByID(tag.TagID);
					if (newtag != null) tags.Add(newtag);
				}
				//tags.Sort();
				return tags;
			}
		}

		[XmlIgnore]
		public List<AniDB_Anime_Tag> AnimeTags
		{
			get
			{
				AniDB_Anime_TagRepository repAnimeTags = new AniDB_Anime_TagRepository();
				return repAnimeTags.GetByAnimeID(AnimeID);
			}
		}

		[XmlIgnore]
		public List<AniDB_Anime_Relation> RelatedAnime
		{
			get
			{
				AniDB_Anime_RelationRepository repRels = new AniDB_Anime_RelationRepository();
				return repRels.GetByAnimeID(AnimeID);
			}
		}

		[XmlIgnore]
		public List<AniDB_Anime_Similar> SimilarAnime
		{
			get
			{
				AniDB_Anime_SimilarRepository rep = new AniDB_Anime_SimilarRepository();
				return rep.GetByAnimeID(AnimeID);
			}
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

		[XmlIgnore]
		public List<AniDB_Anime> AllRelatedAnime
		{
			get
			{
				List<AniDB_Anime> relList = new List<AniDB_Anime>();
				List<int> relListIDs = new List<int>();
				List<int> searchedIDs = new List<int>();

				GetRelatedAnimeRecursive(this.AnimeID, ref relList, ref relListIDs, ref searchedIDs);
				return relList;
			}
		}

		[XmlIgnore]
		public List<AniDB_Anime_Character> AnimeCharacters
		{
			get
			{
				AniDB_Anime_CharacterRepository repRels = new AniDB_Anime_CharacterRepository();
				return repRels.GetByAnimeID(AnimeID);
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

		public List<AniDB_Anime_Title> Titles
		{
			get
			{
				AniDB_Anime_TitleRepository repTitles = new AniDB_Anime_TitleRepository();
				return repTitles.GetByAnimeID(AnimeID);
			}
		}


		public string FormattedTitle
		{
			get
			{
				List<AniDB_Anime_Title> thisTitles = this.Titles;
				foreach (NamingLanguage nlan in Languages.PreferredNamingLanguages)
				{
					string thisLanguage = nlan.Language.Trim().ToUpper();
					
					// Romaji and English titles will be contained in MAIN and/or OFFICIAL
					// we won't use synonyms for these two languages
					if (thisLanguage == "X-JAT" || thisLanguage == "EN")
					{
						foreach (AniDB_Anime_Title title in thisTitles)
						{
							string titleType = title.TitleType.Trim().ToUpper();
							// first try the  Main title
							if (titleType == Constants.AnimeTitleType.Main.ToUpper() && title.Language.Trim().ToUpper() == thisLanguage) return title.Title;
						}
					}

					// now try the official title
					foreach (AniDB_Anime_Title title in thisTitles)
					{
						string titleType = title.TitleType.Trim().ToUpper();
						if (titleType == Constants.AnimeTitleType.Official.ToUpper() && title.Language.Trim().ToUpper() == thisLanguage) return title.Title;
					}

					// try synonyms
					if (ServerSettings.LanguageUseSynonyms)
					{
						foreach (AniDB_Anime_Title title in thisTitles)
						{
							string titleType = title.TitleType.Trim().ToUpper();
							if (titleType == Constants.AnimeTitleType.Synonym.ToUpper() && title.Language.Trim().ToUpper() == thisLanguage) return title.Title;
						}
					}

				}

				// otherwise just use the main title
				return this.MainTitle;
			}
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


		public string PreferredTitle
		{
			get
			{
				List<AniDB_Anime_Title> titles = this.Titles;

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

		public void PopulateAndSaveFromHTTP(Raw_AniDB_Anime animeInfo, List<Raw_AniDB_Episode> eps, List<Raw_AniDB_Anime_Title> titles,
			List<Raw_AniDB_Category> cats, List<Raw_AniDB_Tag> tags, List<Raw_AniDB_Character> chars, List<Raw_AniDB_RelatedAnime> rels, List<Raw_AniDB_SimilarAnime> sims,
			bool downloadRelations)
		{
			Populate(animeInfo);

			// save now for FK purposes
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			repAnime.Save(this);

			CreateEpisodes(eps);
			CreateTitles(titles);
			CreateCategories(cats);
			CreateTags(tags);
			CreateCharacters(chars);
			CreateRelations(rels, downloadRelations);
			CreateSimilarAnime(sims);
			
			repAnime.Save(this);
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

		private void CreateEpisodes(List<Raw_AniDB_Episode> eps)
		{
			if (eps == null) return;

			AniDB_EpisodeRepository repEps = new AniDB_EpisodeRepository();

			this.EpisodeCountSpecial = 0;
			this.EpisodeCountNormal = 0;

			foreach (Raw_AniDB_Episode epraw in eps)
			{
				List<AniDB_Episode> existingEps = repEps.GetByAnimeIDAndEpisodeTypeNumber(epraw.AnimeID, (enEpisodeType)epraw.EpisodeType, epraw.EpisodeNumber);
				// we need to do this check because some times AniDB will replace an existing episode with a new episode

				// delete any old records
				foreach (AniDB_Episode epOld in existingEps)
				{
					if (epOld.EpisodeID != epraw.EpisodeID)
					{
						// first delete any AnimeEpisode records that point to the new anidb episode
						AnimeEpisodeRepository repAnimeEps = new AnimeEpisodeRepository();
						AnimeEpisode aniep = repAnimeEps.GetByAniDBEpisodeID(epOld.EpisodeID);
						if (aniep != null)
							repAnimeEps.Delete(aniep.AnimeEpisodeID);

						repEps.Delete(epOld.AniDB_EpisodeID);
					}
				}


				AniDB_Episode ep = repEps.GetByEpisodeID(epraw.EpisodeID);
				if (ep == null) ep = new AniDB_Episode();

				ep.Populate(epraw);
				repEps.Save(ep);

				// since the HTTP api doesn't return a count of the number of specials, we will calculate it here
				if (ep.EpisodeTypeEnum == AniDBAPI.enEpisodeType.Episode)
					this.EpisodeCountNormal++;

				if (ep.EpisodeTypeEnum == AniDBAPI.enEpisodeType.Special)
					this.EpisodeCountSpecial++;
			}

			this.EpisodeCount = EpisodeCountSpecial + EpisodeCountNormal;
		}

		private void CreateTitles(List<Raw_AniDB_Anime_Title> titles)
		{
			if (titles == null) return;

			this.AllTitles = "";

			// first delete all existing titles for this anime
			AniDB_Anime_TitleRepository repTitles = new AniDB_Anime_TitleRepository();
			List<AniDB_Anime_Title> existingtitles = repTitles.GetByAnimeID(this.AnimeID);
			foreach (AniDB_Anime_Title animetitle in existingtitles)
			{
				repTitles.Delete(animetitle.AniDB_Anime_TitleID);
			}

			foreach (Raw_AniDB_Anime_Title rawtitle in titles)
			{
				AniDB_Anime_Title title = new AniDB_Anime_Title();
				title.Populate(rawtitle);
				repTitles.Save(title);

				if (this.AllTitles.Length > 0) this.AllTitles += "|";
				this.AllTitles += rawtitle.Title;
			}
		}

		private void CreateCategories(List<Raw_AniDB_Category> cats)
		{
			if (cats == null) return;

			this.AllCategories = "";

			AniDB_CategoryRepository repCats = new AniDB_CategoryRepository();
			AniDB_Anime_CategoryRepository repXRefs = new AniDB_Anime_CategoryRepository();

			foreach (Raw_AniDB_Category rawcat in cats)
			{
				AniDB_Category cat = repCats.GetByCategoryID(rawcat.CategoryID);
				if (cat == null) cat = new AniDB_Category();

				cat.Populate(rawcat);
				repCats.Save(cat);

				AniDB_Anime_Category anime_cat = repXRefs.GetByAnimeIDAndCategoryID(rawcat.AnimeID, rawcat.CategoryID);
				if (anime_cat == null) anime_cat = new AniDB_Anime_Category();

				anime_cat.Populate(rawcat);
				repXRefs.Save(anime_cat);

				if (this.AllCategories.Length > 0) this.AllCategories += "|";
				this.AllCategories += cat.CategoryName;
			}
		}

		private void CreateTags(List<Raw_AniDB_Tag> tags)
		{
			if (tags == null) return;

			this.AllTags = "";

			AniDB_TagRepository repTags = new AniDB_TagRepository();
			AniDB_Anime_TagRepository repTagsXRefs = new AniDB_Anime_TagRepository();

			foreach (Raw_AniDB_Tag rawtag in tags)
			{
				AniDB_Tag tag = repTags.GetByTagID(rawtag.TagID);
				if (tag == null) tag = new AniDB_Tag();

				tag.Populate(rawtag);
				repTags.Save(tag);

				AniDB_Anime_Tag anime_tag = repTagsXRefs.GetByAnimeIDAndTagID(rawtag.AnimeID, rawtag.TagID);
				if (anime_tag == null) anime_tag = new AniDB_Anime_Tag();

				anime_tag.Populate(rawtag);
				repTagsXRefs.Save(anime_tag);

				if (this.AllTags.Length > 0) this.AllTags += "|";
				this.AllTags += tag.TagName;
			}
		}

		private void CreateCharacters(List<Raw_AniDB_Character> chars)
		{
			if (chars == null) return;

			AniDB_CharacterRepository repChars = new AniDB_CharacterRepository();
			AniDB_Anime_CharacterRepository repAnimeChars = new AniDB_Anime_CharacterRepository();
			AniDB_Character_SeiyuuRepository repCharSeiyuu = new AniDB_Character_SeiyuuRepository();
			AniDB_SeiyuuRepository repSeiyuu = new AniDB_SeiyuuRepository();

			// delete all the existing cross references just in case one has been removed
			List<AniDB_Anime_Character> animeChars = repAnimeChars.GetByAnimeID(AnimeID);
			foreach (AniDB_Anime_Character xref in animeChars)
				repAnimeChars.Delete(xref.AniDB_Anime_CharacterID);
			

			foreach (Raw_AniDB_Character rawchar in chars)
			{
				AniDB_Character chr = repChars.GetByCharID(rawchar.CharID);
				if (chr == null)
					chr = new AniDB_Character();

				// delete existing relationships to seiyuu's
				List<AniDB_Character_Seiyuu> allCharSei = repCharSeiyuu.GetByCharID(rawchar.CharID);
				foreach (AniDB_Character_Seiyuu xref in allCharSei)
					repCharSeiyuu.Delete(xref.AniDB_Character_SeiyuuID);

				chr.PopulateFromHTTP(rawchar);
				repChars.Save(chr);

				// create cross ref's between anime and character, but don't actually download anything
				AniDB_Anime_Character anime_char = new AniDB_Anime_Character();
				anime_char.Populate(rawchar);
				repAnimeChars.Save(anime_char);

				foreach (Raw_AniDB_Seiyuu rawSeiyuu in rawchar.Seiyuus)
				{
					// save the link between character and seiyuu
					AniDB_Character_Seiyuu acc = repCharSeiyuu.GetByCharIDAndSeiyuuID(rawchar.CharID, rawSeiyuu.SeiyuuID);
					if (acc == null)
					{
						acc = new AniDB_Character_Seiyuu();
						acc.CharID = chr.CharID;
						acc.SeiyuuID = rawSeiyuu.SeiyuuID;
						repCharSeiyuu.Save(acc);
					}

					// save the seiyuu
					AniDB_Seiyuu seiyuu = repSeiyuu.GetBySeiyuuID(rawSeiyuu.SeiyuuID);
					if (seiyuu == null) seiyuu = new AniDB_Seiyuu();
					seiyuu.PicName = rawSeiyuu.PicName;
					seiyuu.SeiyuuID = rawSeiyuu.SeiyuuID;
					seiyuu.SeiyuuName = rawSeiyuu.SeiyuuName;
					repSeiyuu.Save(seiyuu);
				}
			}
		}

		private void CreateRelations(List<Raw_AniDB_RelatedAnime> rels, bool downloadRelations)
		{
			if (rels == null) return;

			AniDB_Anime_RelationRepository repRels = new AniDB_Anime_RelationRepository();

			foreach (Raw_AniDB_RelatedAnime rawrel in rels)
			{
				AniDB_Anime_Relation anime_rel = repRels.GetByAnimeIDAndRelationID(rawrel.AnimeID, rawrel.RelatedAnimeID);
				if (anime_rel == null) anime_rel = new AniDB_Anime_Relation();

				anime_rel.Populate(rawrel);
				repRels.Save(anime_rel);

				if (ServerSettings.AniDB_DownloadRelatedAnime && downloadRelations && ServerSettings.AutoGroupSeries)
				{
					logger.Info("Adding command to download related anime for {0} ({1}), related anime ID = {2}",
						this.MainTitle, this.AnimeID, rawrel.RelatedAnimeID);

					// I have disable the downloading of relations here because of banning issues
					// basically we will download immediate relations, but not relations of relations

					//CommandRequest_GetAnimeHTTP cr_anime = new CommandRequest_GetAnimeHTTP(rawrel.RelatedAnimeID, false, downloadRelations);
					CommandRequest_GetAnimeHTTP cr_anime = new CommandRequest_GetAnimeHTTP(rawrel.RelatedAnimeID, false, false);
					cr_anime.Save();
				}
			}
		}

		private void CreateSimilarAnime(List<Raw_AniDB_SimilarAnime> sims)
		{
			if (sims == null) return;

			AniDB_Anime_SimilarRepository repSim = new AniDB_Anime_SimilarRepository();

			foreach (Raw_AniDB_SimilarAnime rawsim in sims)
			{
				AniDB_Anime_Similar anime_sim = repSim.GetByAnimeIDAndSimilarID(rawsim.AnimeID, rawsim.SimilarAnimeID);
				if (anime_sim == null) anime_sim = new AniDB_Anime_Similar();

				anime_sim.Populate(rawsim);
				repSim.Save(anime_sim);

				// Have commented this out for now, because it ends up downloading most of the database
				/*
				if (ServerSettings.AniDB_DownloadRelatedAnime)
				{
					logger.Info("Adding command to download similar anime for {0} ({1}), similar anime ID = {2}",
						this.MainTitle, this.AnimeID, rawsim.SimilarAnimeID);
					CommandRequest_GetAnimeHTTP cr_anime = new CommandRequest_GetAnimeHTTP(rawsim.SimilarAnimeID, false);
					cr_anime.Priority = (int)CommandRequestPriority.Priority10; // set as low priority, so that we get info about animes we actually have first
					cr_anime.Save();
				}*/
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

		public Contract_AniDBAnime ToContract(bool getDefaultImages)
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
			contract.FormattedTitle = this.FormattedTitle;

			if (getDefaultImages)
			{
				AniDB_Anime_DefaultImage defFanart = this.DefaultFanart;
				if (defFanart != null) contract.DefaultImageFanart = defFanart.ToContract();

				AniDB_Anime_DefaultImage defPoster = this.DefaultPoster;
				if (defPoster != null) contract.DefaultImagePoster = defPoster.ToContract();

				AniDB_Anime_DefaultImage defBanner = this.DefaultWideBanner;
				if (defBanner != null) contract.DefaultImageWideBanner = defBanner.ToContract();
			}

			return contract;
		}

		public Contract_AniDBAnime ToContract()
		{
			return ToContract(false);
		}

		public Contract_AniDB_AnimeDetailed ToContractDetailed()
		{
			AniDB_Anime_TitleRepository repTitles = new AniDB_Anime_TitleRepository();
			AniDB_CategoryRepository repCats = new AniDB_CategoryRepository();
			AniDB_TagRepository repTags = new AniDB_TagRepository();

			Contract_AniDB_AnimeDetailed contract = new Contract_AniDB_AnimeDetailed();

			contract.AnimeTitles = new List<Contract_AnimeTitle>();
			contract.Categories = new List<Contract_AnimeCategory>();
			contract.Tags = new List<Contract_AnimeTag>();

			contract.AniDBAnime = this.ToContract();

			// get all the anime titles
			List<AniDB_Anime_Title> animeTitles = repTitles.GetByAnimeID(AnimeID);
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


			foreach (AniDB_Anime_Category animeCat in AnimeCategories)
			{
				AniDB_Category cat = repCats.GetByCategoryID(animeCat.CategoryID);
				if (cat != null)
				{
					Contract_AnimeCategory ccat = new Contract_AnimeCategory();
					ccat.CategoryDescription = cat.CategoryDescription;
					ccat.CategoryID = cat.CategoryID;
					ccat.CategoryName = cat.CategoryName;
					ccat.IsHentai = cat.IsHentai;
					ccat.ParentID = cat.ParentID;
					ccat.Weighting = animeCat.Weighting;
					contract.Categories.Add(ccat);
				}
			}

			foreach (AniDB_Anime_Tag animeTag in AnimeTags)
			{
				AniDB_Tag tag = repTags.GetByTagID(animeTag.TagID);
				if (tag != null)
				{
					Contract_AnimeTag ctag = new Contract_AnimeTag();
					ctag.Approval = animeTag.Approval;
					ctag.GlobalSpoiler = tag.GlobalSpoiler;
					ctag.LocalSpoiler = tag.LocalSpoiler;
					ctag.Spoiler = tag.Spoiler;
					ctag.TagCount = tag.TagCount;
					ctag.TagDescription = tag.TagDescription;
					ctag.TagID = tag.TagID;
					ctag.TagName = tag.TagName;
					contract.Tags.Add(ctag);
				}
			}

			if (this.UserVote != null)
				contract.UserVote = this.UserVote.ToContract();

			AdhocRepository repAdHoc = new AdhocRepository();
			List<string> audioLanguages = new List<string>();
			List<string> subtitleLanguages = new List<string>();

			// audio languages
			Dictionary<int, LanguageStat> dicAudio = repAdHoc.GetAudioLanguageStatsByAnime(this.AnimeID);
			foreach (KeyValuePair<int, LanguageStat> kvp in dicAudio)
			{
				foreach (string lanName in kvp.Value.LanguageNames)
				{
					if (!audioLanguages.Contains(lanName))
						audioLanguages.Add(lanName);
				}
			}

			// subtitle languages
			Dictionary<int, LanguageStat> dicSubtitle = repAdHoc.GetSubtitleLanguageStatsByAnime(this.AnimeID);
			foreach (KeyValuePair<int, LanguageStat> kvp in dicSubtitle)
			{
				foreach (string lanName in kvp.Value.LanguageNames)
				{
					if (!subtitleLanguages.Contains(lanName))
						subtitleLanguages.Add(lanName);
				}
			}

			contract.Stat_AudioLanguages = "";
			foreach (string audioLan in audioLanguages)
			{
				if (contract.Stat_AudioLanguages.Length > 0) contract.Stat_AudioLanguages += ",";
				contract.Stat_AudioLanguages += audioLan;
			}

			contract.Stat_SubtitleLanguages = "";
			foreach (string subLan in subtitleLanguages)
			{
				if (contract.Stat_SubtitleLanguages.Length > 0) contract.Stat_SubtitleLanguages += ",";
				contract.Stat_SubtitleLanguages += subLan;
			}

			contract.Stat_AllVideoQuality = repAdHoc.GetAllVideoQualityForAnime(this.AnimeID);

			contract.Stat_AllVideoQuality_Episodes = "";
			AnimeVideoQualityStat stat = repAdHoc.GetEpisodeVideoQualityStatsForAnime(this.AnimeID);
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

			return contract;
		}

		public AnimeSeries CreateAnimeSeriesAndGroup()
		{
			// create a new AnimeSeries record
			AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
			AnimeGroupRepository repGroups = new AnimeGroupRepository();

			AnimeSeries ser = new AnimeSeries();
			ser.Populate(this);

			JMMUserRepository repUsers = new JMMUserRepository();
			List<JMMUser> allUsers = repUsers.GetAll();

			// create the AnimeGroup record
			// check if there are any existing groups we could add this series to
			bool createNewGroup = true;

			if (ServerSettings.AutoGroupSeries)
			{
				List<AnimeGroup> grps = AnimeGroup.GetRelatedGroupsFromAnimeID(ser.AniDB_ID);

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
			CommandRequest_TraktSearchAnime cmd2 = new CommandRequest_TraktSearchAnime(this.AnimeID, false);
			cmd2.Save();

			return ser;
		}

		public static void GetRelatedAnimeRecursive(int animeID, ref List<AniDB_Anime> relList, ref List<int> relListIDs, ref List<int> searchedIDs)
		{
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
			AniDB_Anime anime = repAnime.GetByAnimeID(animeID);
			searchedIDs.Add(animeID);

			foreach (AniDB_Anime_Relation rel in anime.RelatedAnime)
			{
				AniDB_Anime relAnime = repAnime.GetByAnimeID(rel.RelatedAnimeID);
				if (relAnime!= null && !relListIDs.Contains(relAnime.AnimeID))
				{
					relList.Add(relAnime);
					relListIDs.Add(relAnime.AnimeID);
					if (!searchedIDs.Contains(rel.RelatedAnimeID))
					{
						GetRelatedAnimeRecursive(rel.RelatedAnimeID, ref relList, ref relListIDs, ref searchedIDs);
					}
				}
			}
		}
	}
}
