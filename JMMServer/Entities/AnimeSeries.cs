using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using JMMServer.Repositories;
using JMMContracts;

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
		#endregion

		public string Year
		{
			get
			{
				return Anime.Year;
			}
		}

		private static Logger logger = LogManager.GetCurrentClassLogger();


		public string GenresRaw
		{
			get
			{
				if (Anime == null)
					return "";
				else
					return Anime.CategoriesString;
			}
		}

		public List<AnimeEpisode> AnimeEpisodes
		{
			get
			{
				AnimeEpisodeRepository repEpisodes = new AnimeEpisodeRepository();
				return repEpisodes.GetBySeriesID(AnimeSeriesID);
			}
		}

		public CrossRef_AniDB_TvDB CrossRefTvDB
		{
			get
			{
				CrossRef_AniDB_TvDBRepository repCrossRef = new CrossRef_AniDB_TvDBRepository();
				return repCrossRef.GetByAnimeID(this.AniDB_ID);
			}
		}

		public CrossRef_AniDB_Trakt CrossRefTrakt
		{
			get
			{
				CrossRef_AniDB_TraktRepository repCrossRef = new CrossRef_AniDB_TraktRepository();
				return repCrossRef.GetByAnimeID(this.AniDB_ID);
			}
		}

		public CrossRef_AniDB_Other CrossRefMovieDB
		{
			get
			{
				CrossRef_AniDB_OtherRepository repCrossRef = new CrossRef_AniDB_OtherRepository();
				return repCrossRef.GetByAnimeIDAndType(this.AniDB_ID, CrossRefType.MovieDB);
			}
		}

		public AnimeEpisode GetLastEpisodeWatched(int userID)
		{
			AnimeEpisode watchedep = null;
			AnimeEpisode_User userRecordWatched = null;

			foreach (AnimeEpisode ep in AnimeEpisodes)
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
			AnimeSeries_UserRepository repUser = new AnimeSeries_UserRepository();
			return repUser.GetByUserAndSeriesID(userID, this.AnimeSeriesID);
		}

		public AniDB_Anime Anime
		{
			get
			{
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				AniDB_Anime anidb_anime = repAnime.GetByAnimeID(this.AniDB_ID);
				return anidb_anime;
			}
		}

		public DateTime? AirDate
		{
			get
			{
				if (Anime != null)
					return Anime.AirDate;
				return DateTime.Now;

			}
		}

		public DateTime? EndDate
		{
			get
			{
				if (Anime != null)
					return Anime.EndDate;
				return null;

			}
		}

		public void Populate(AniDB_Anime anime)
		{
			this.AniDB_ID = anime.AnimeID;
			this.LatestLocalEpisodeNumber = 0;
			this.DateTimeUpdated = DateTime.Now;
			this.DateTimeCreated = DateTime.Now;
		}

		public void CreateAnimeEpisodes()
		{
			AniDB_Anime anime = Anime;
			if (anime == null) return;

			foreach (AniDB_Episode ep in anime.AniDBEpisodes)
			{
				ep.CreateAnimeEpisode(this.AnimeSeriesID);
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

		public Contract_AnimeSeries ToContract(AnimeSeries_User userRecord)
		{
			AniDB_Anime anime = this.Anime;
			CrossRef_AniDB_TvDB tvDBCrossRef = this.CrossRefTvDB;
			CrossRef_AniDB_Other movieDBCrossRef = this.CrossRefMovieDB;

			return this.ToContract(anime, tvDBCrossRef, movieDBCrossRef, userRecord);
		}

		public Contract_AnimeSeries ToContract(AniDB_Anime animeRec, CrossRef_AniDB_TvDB tvDBCrossRef, CrossRef_AniDB_Other movieDBCrossRef, AnimeSeries_User userRecord)
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
				Contract_AniDBAnime animecontract = animeRec.ToContract();

				AniDB_Anime_DefaultImage defaultPoster = animeRec.DefaultPoster;
				if (defaultPoster == null)
					animecontract.DefaultImagePoster = null;
				else
					animecontract.DefaultImagePoster = defaultPoster.ToContract();

				AniDB_Anime_DefaultImage defaultFanart = animeRec.DefaultFanart;
				if (defaultFanart == null)
					animecontract.DefaultImageFanart = null;
				else
					animecontract.DefaultImageFanart = defaultFanart.ToContract();

				AniDB_Anime_DefaultImage defaultWideBanner = animeRec.DefaultWideBanner;
				if (defaultWideBanner == null)
					animecontract.DefaultImageWideBanner = null;
				else
					animecontract.DefaultImageWideBanner = defaultWideBanner.ToContract();

				contract.AniDBAnime = animecontract;
			}

			contract.CrossRefAniDBTvDB = null;
			if (tvDBCrossRef != null)
				contract.CrossRefAniDBTvDB = tvDBCrossRef.ToContract();

			contract.CrossRefAniDBMovieDB = null;
			if (movieDBCrossRef != null)
				contract.CrossRefAniDBMovieDB = movieDBCrossRef.ToContract();

			return contract;
		}

		public override string ToString()
		{
			return string.Format("Series: {0} ({1})", Anime.MainTitle, AnimeSeriesID);
			//return "";
		}

		public void UpdateStats(bool watchedStats, bool missingEpsStats, bool updateAllGroupsAbove)
		{
			
			DateTime start = DateTime.Now;

			AnimeSeries_UserRepository repSeriesUser = new AnimeSeries_UserRepository();

			JMMUserRepository repUsers = new JMMUserRepository();
			List<JMMUser> allUsers = repUsers.GetAll();

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
					userRecord.WatchedDate = null;

					foreach (AnimeEpisode ep in AnimeEpisodes)
					{
						// if the episode doesn't have any files then it won't count towards watched/unwatched counts
						if (ep.VideoLocals.Count == 0) continue;

						if (ep.EpisodeTypeEnum == AniDBAPI.enEpisodeType.Episode || ep.EpisodeTypeEnum == AniDBAPI.enEpisodeType.Special)
						{
							AnimeEpisode_User epUserRecord = ep.GetUserRecord(juser.JMMUserID);

							if (epUserRecord != null) userRecord.WatchedEpisodeCount++;
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
							}
						}
					}
					repSeriesUser.Save(userRecord);

				}
			}

			if (missingEpsStats)
			{
				MissingEpisodeCount = 0;
				MissingEpisodeCountGroups = 0;

				// get all the group status records
				AniDB_GroupStatusRepository repGrpStat = new AniDB_GroupStatusRepository();
				List<AniDB_GroupStatus> grpStatuses = repGrpStat.GetByAnimeID(this.AniDB_ID);
				List<AnimeEpisode> eps = AnimeEpisodes;

				// find all the episodes for which the user has a file
				// from this we can determine what their latest episode number is
				// find out which groups the user is collecting

				List<int> userReleaseGroups = new List<int>();
				foreach (AnimeEpisode ep in eps)
				{
					List<VideoLocal> vids = ep.VideoLocals;
					foreach (VideoLocal vid in vids)
					{
						AniDB_File anifile = vid.AniDBFile;
						if (anifile != null)
						{
							if (!userReleaseGroups.Contains(anifile.GroupID)) userReleaseGroups.Add(anifile.GroupID);
						}
					}
				}

				int latestLocalEpNumber = 0;
				foreach (AnimeEpisode ep in eps)
				{
					List<VideoLocal> vids = ep.VideoLocals;
					if (ep.EpisodeTypeEnum != AniDBAPI.enEpisodeType.Episode) continue;

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
						if (userReleaseGroups.Contains(gs.GroupID) && gs.HasGroupReleasedEpisode(thisEpNum)) epReleasedGroup = true;
					}

					if (epReleased && vids.Count == 0) 
						MissingEpisodeCount++;
					if (epReleasedGroup && vids.Count == 0) 
						MissingEpisodeCountGroups++;
				}
				this.LatestLocalEpisodeNumber = latestLocalEpNumber;
			}

			AnimeSeriesRepository rep = new AnimeSeriesRepository();
			rep.Save(this);

			if (updateAllGroupsAbove)
			{
				foreach (AnimeGroup grp in AllGroupsAbove)
				{
					grp.UpdateStats(watchedStats, missingEpsStats);
				}
			}
			TimeSpan ts = DateTime.Now - start;
			logger.Trace("Updated stats for SERIES {0} in {1}ms", this.ToString(), ts.TotalMilliseconds);
		}

	}
}
