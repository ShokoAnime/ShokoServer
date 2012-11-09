using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
using JMMServer.Repositories;
using JMMServer.Entities;
using NLog;
using System.Collections;
using JMMServer.Databases;
using System.IO;
using System.ServiceModel.Web;
using JMMServer.ImageDownload;
using BinaryNorthwest;
using AniDBAPI;
using JMMServer.Providers.TraktTV;

namespace JMMServer
{
	public class JMMServiceImplementationMetro : IJMMServerMetro
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public Contract_ServerStatus GetServerStatus()
		{
			Contract_ServerStatus contract = new Contract_ServerStatus();

			try
			{
				contract.HashQueueCount = JMMService.CmdProcessorHasher.QueueCount;
				contract.HashQueueState = JMMService.CmdProcessorHasher.QueueState;

				contract.GeneralQueueCount = JMMService.CmdProcessorGeneral.QueueCount;
				contract.GeneralQueueState = JMMService.CmdProcessorGeneral.QueueState;

				contract.ImagesQueueCount = JMMService.CmdProcessorImages.QueueCount;
				contract.ImagesQueueState = JMMService.CmdProcessorImages.QueueState;

				contract.IsBanned = JMMService.AnidbProcessor.IsBanned;
				contract.BanReason = JMMService.AnidbProcessor.BanTime.ToString();
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
			return contract;
		}

		public Contract_JMMUser AuthenticateUser(string username, string password)
		{
			JMMUserRepository repUsers = new JMMUserRepository();

			try
			{
				JMMUser user = repUsers.AuthenticateUser(username, password);
				if (user == null) return null;

				return user.ToContract();

			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return null;
			}
		}

		public List<Contract_JMMUser> GetAllUsers()
		{
			JMMUserRepository repUsers = new JMMUserRepository();

			// get all the users
			List<Contract_JMMUser> userList = new List<Contract_JMMUser>();

			try
			{
				List<JMMUser> users = repUsers.GetAll();
				foreach (JMMUser user in users)
					userList.Add(user.ToContract());

			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
			return userList;
		}

		public List<Contract_AnimeGroup> GetAllGroups(int userID)
		{
			List<Contract_AnimeGroup> grps = new List<Contract_AnimeGroup>();
			try
			{
				AnimeGroupRepository repGroups = new AnimeGroupRepository();
				AnimeGroup_UserRepository repUserGroups = new AnimeGroup_UserRepository();

				List<AnimeGroup> allGrps = repGroups.GetAll();

				// user records
				AnimeGroup_UserRepository repGroupUser = new AnimeGroup_UserRepository();
				List<AnimeGroup_User> userRecordList = repGroupUser.GetByUserID(userID);
				Dictionary<int, AnimeGroup_User> dictUserRecords = new Dictionary<int, AnimeGroup_User>();
				foreach (AnimeGroup_User grpUser in userRecordList)
					dictUserRecords[grpUser.AnimeGroupID] = grpUser;

				foreach (AnimeGroup ag in allGrps)
				{
					AnimeGroup_User userRec = null;
					if (dictUserRecords.ContainsKey(ag.AnimeGroupID))
						userRec = dictUserRecords[ag.AnimeGroupID];

					// calculate stats
					Contract_AnimeGroup contract = ag.ToContract(userRec);
					contract.ServerPosterPath = ag.PosterPathNoBlanks;
					grps.Add(contract);
				}

				grps.Sort();

			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
			return grps;
		}

		public List<Contract_AnimeEpisode> GetEpisodesRecentlyAddedSummary(int maxRecords, int jmmuserID)
		{
			List<Contract_AnimeEpisode> retEps = new List<Contract_AnimeEpisode>();
			try
			{
				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				AnimeEpisode_UserRepository repEpUser = new AnimeEpisode_UserRepository();
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				JMMUserRepository repUsers = new JMMUserRepository();
				VideoLocalRepository repVids = new VideoLocalRepository();

				JMMUser user = repUsers.GetByID(jmmuserID);
				if (user == null) return retEps;

				string sql = "Select ae.AnimeSeriesID, max(vl.DateTimeCreated) as MaxDate " +
						"From VideoLocal vl " +
						"INNER JOIN CrossRef_File_Episode xref ON vl.Hash = xref.Hash " +
						"INNER JOIN AnimeEpisode ae ON ae.AniDB_EpisodeID = xref.EpisodeID " +
						"GROUP BY ae.AnimeSeriesID " +
						"ORDER BY MaxDate desc ";
				ArrayList results = DatabaseHelper.GetData(sql);

				int numEps = 0;
				foreach (object[] res in results)
				{
					int animeSeriesID = int.Parse(res[0].ToString());

					AnimeSeries ser = repSeries.GetByID(animeSeriesID);
					if (ser == null) continue;

					if (!user.AllowedSeries(ser)) continue;

					List<VideoLocal> vids = repVids.GetMostRecentlyAddedForAnime(1, ser.AniDB_ID);
					if (vids.Count == 0) continue;

					List<AnimeEpisode> eps = vids[0].AnimeEpisodes;
					if (eps.Count == 0) continue;

					Contract_AnimeEpisode epContract = eps[0].ToContract(jmmuserID);
					if (epContract != null)
					{
						retEps.Add(epContract);
						numEps++;

						// Lets only return the specified amount
						if (retEps.Count == maxRecords) return retEps;
					}


				}
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}

			return retEps;
		}

		public List<MetroContract_Anime_Summary> GetAnimeWithNewEpisodes(int maxRecords, int jmmuserID)
		{
			List<MetroContract_Anime_Summary> retAnime = new List<MetroContract_Anime_Summary>();
			try
			{
				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				AnimeEpisode_UserRepository repEpUser = new AnimeEpisode_UserRepository();
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				JMMUserRepository repUsers = new JMMUserRepository();
				VideoLocalRepository repVids = new VideoLocalRepository();

				JMMUser user = repUsers.GetByID(jmmuserID);
				if (user == null) return retAnime;

				string sql = "Select ae.AnimeSeriesID, max(vl.DateTimeCreated) as MaxDate " +
						"From VideoLocal vl " +
						"INNER JOIN CrossRef_File_Episode xref ON vl.Hash = xref.Hash " +
						"INNER JOIN AnimeEpisode ae ON ae.AniDB_EpisodeID = xref.EpisodeID " +
						"GROUP BY ae.AnimeSeriesID " +
						"ORDER BY MaxDate desc ";
				ArrayList results = DatabaseHelper.GetData(sql);

				int numEps = 0;
				foreach (object[] res in results)
				{
					int animeSeriesID = int.Parse(res[0].ToString());

					AnimeSeries ser = repSeries.GetByID(animeSeriesID);
					if (ser == null) continue;

					if (!user.AllowedSeries(ser)) continue;

					List<VideoLocal> vids = repVids.GetMostRecentlyAddedForAnime(1, ser.AniDB_ID);
					if (vids.Count == 0) continue;

					List<AnimeEpisode> eps = vids[0].AnimeEpisodes;
					if (eps.Count == 0) continue;

					Contract_AnimeEpisode epContract = eps[0].ToContract(jmmuserID);
					if (epContract != null)
					{
						AniDB_Anime anidb_anime = ser.Anime;

						MetroContract_Anime_Summary summ = new MetroContract_Anime_Summary();
						summ.AnimeID = ser.AniDB_ID;
						summ.AnimeName = ser.SeriesName;
						summ.AnimeSeriesID = ser.AnimeSeriesID;
						summ.BeginYear = anidb_anime.BeginYear;
						summ.EndYear = anidb_anime.EndYear;
						summ.PosterName = anidb_anime.DefaultPosterPathNoBlanks;

						ImageDetails imgDet = anidb_anime.DefaultPosterDetailsNoBlanks;
						summ.ImageType = (int)imgDet.ImageType;
						summ.ImageID = imgDet.ImageID;

						retAnime.Add(summ);
						numEps++;

						// Lets only return the specified amount
						if (retAnime.Count == maxRecords) return retAnime;
					}


				}
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}

			return retAnime;
		}

		public List<MetroContract_Anime_Summary> GetAnimeContinueWatching(int maxRecords, int jmmuserID)
		{
			List<MetroContract_Anime_Summary> retAnime = new List<MetroContract_Anime_Summary>();
			try
			{
				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				AnimeSeriesRepository repAnimeSer = new AnimeSeriesRepository();
				AnimeSeries_UserRepository repSeriesUser = new AnimeSeries_UserRepository();
				JMMUserRepository repUsers = new JMMUserRepository();

				DateTime start = DateTime.Now;

				JMMUser user = repUsers.GetByID(jmmuserID);
				if (user == null) return retAnime;

				// get a list of series that is applicable
				List<AnimeSeries_User> allSeriesUser = repSeriesUser.GetMostRecentlyWatched(jmmuserID);

				TimeSpan ts = DateTime.Now - start;
				logger.Info(string.Format("GetAnimeContinueWatching:Series: {0}", ts.TotalMilliseconds));
				start = DateTime.Now;

				JMMServiceImplementation imp = new JMMServiceImplementation();
				foreach (AnimeSeries_User userRecord in allSeriesUser)
				{
					AnimeSeries series = repAnimeSer.GetByID(userRecord.AnimeSeriesID);
					if (series == null) continue;

					if (!user.AllowedSeries(series)) continue;

					Contract_AnimeEpisode ep = imp.GetNextUnwatchedEpisode(userRecord.AnimeSeriesID, jmmuserID);
					if (ep != null)
					{
						AniDB_Anime anidb_anime = series.Anime;

						MetroContract_Anime_Summary summ = new MetroContract_Anime_Summary();
						summ.AnimeID = series.AniDB_ID;
						summ.AnimeName = series.SeriesName;
						summ.AnimeSeriesID = series.AnimeSeriesID;
						summ.BeginYear = anidb_anime.BeginYear;
						summ.EndYear = anidb_anime.EndYear;
						summ.PosterName = anidb_anime.DefaultPosterPathNoBlanks;

						ImageDetails imgDet = anidb_anime.DefaultPosterDetailsNoBlanks;
						summ.ImageType = (int)imgDet.ImageType;
						summ.ImageID = imgDet.ImageID;

						retAnime.Add(summ);


						// Lets only return the specified amount
						if (retAnime.Count == maxRecords) return retAnime;
					}
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}

			return retAnime;
		}

		public MetroContract_Anime_Detail GetAnimeDetail(int animeID, int jmmuserID, int maxEpisodeRecords)
		{
			try
			{
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();

				AniDB_Anime anime = repAnime.GetByAnimeID(animeID);
				if (anime == null) return null;

				AnimeSeries ser = repSeries.GetByAnimeID(animeID);

				MetroContract_Anime_Detail ret = new MetroContract_Anime_Detail();
				ret.AnimeID = anime.AnimeID;

				if (ser != null)
					ret.AnimeName = ser.SeriesName;
				else
					ret.AnimeName = anime.MainTitle;

				if (ser != null)
					ret.AnimeSeriesID = ser.AnimeSeriesID;
				else
					ret.AnimeSeriesID = 0;

				ret.BeginYear = anime.BeginYear;
				ret.EndYear = anime.EndYear;

				ImageDetails imgDet = anime.DefaultPosterDetailsNoBlanks;
				ret.PosterImageType = (int)imgDet.ImageType;
				ret.PosterImageID = imgDet.ImageID;

				ImageDetails imgDetFan = anime.DefaultFanartDetailsNoBlanks;
				if (imgDetFan != null)
				{
					ret.FanartImageType = (int)imgDetFan.ImageType;
					ret.FanartImageID = imgDetFan.ImageID;
				}
				else
				{
					ret.FanartImageType = 0;
					ret.FanartImageID = 0;
				}

				ret.AnimeType = anime.AnimeTypeDescription;
				ret.Description = anime.Description;
				ret.EpisodeCountNormal = anime.EpisodeCountNormal;
				ret.EpisodeCountSpecial = anime.EpisodeCountSpecial;
				

				ret.AirDate = anime.AirDate;
				ret.EndDate = anime.EndDate;

				ret.OverallRating = anime.AniDBRating;
				ret.TotalVotes = anime.AniDBTotalVotes;
				ret.AllCategories = anime.CategoriesString;

				ret.NextEpisodesToWatch = new List<MetroContract_Anime_Episode>();
				if (ser != null)
				{
					AnimeSeries_User serUserRec = ser.GetUserRecord(jmmuserID);
					if (ser != null)
						ret.UnwatchedEpisodeCount = serUserRec.UnwatchedEpisodeCount;
					else
						ret.UnwatchedEpisodeCount = 0;

					AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
					AnimeEpisode_UserRepository repEpUser = new AnimeEpisode_UserRepository();

					List<AnimeEpisode> epList = new List<AnimeEpisode>();
					Dictionary<int, AnimeEpisode_User> dictEpUsers = new Dictionary<int, AnimeEpisode_User>();
					foreach (AnimeEpisode_User userRecord in repEpUser.GetByUserIDAndSeriesID(jmmuserID, ser.AnimeSeriesID))
						dictEpUsers[userRecord.AnimeEpisodeID] = userRecord;

					foreach (AnimeEpisode animeep in repEps.GetBySeriesID(ser.AnimeSeriesID))
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

					AniDB_EpisodeRepository repAniEps = new AniDB_EpisodeRepository();
					List<AniDB_Episode> aniEpList = repAniEps.GetByAnimeID(ser.AniDB_ID);
					Dictionary<int, AniDB_Episode> dictAniEps = new Dictionary<int, AniDB_Episode>();
					foreach (AniDB_Episode aniep in aniEpList)
						dictAniEps[aniep.EpisodeID] = aniep;

					List<Contract_AnimeEpisode> candidateEps = new List<Contract_AnimeEpisode>();
					
					foreach (AnimeEpisode ep in epList)
					{
						if (dictAniEps.ContainsKey(ep.AniDB_EpisodeID))
						{
							AniDB_Episode anidbep = dictAniEps[ep.AniDB_EpisodeID];
							if (anidbep.EpisodeType == (int)enEpisodeType.Episode || anidbep.EpisodeType == (int)enEpisodeType.Special)
							{
								AnimeEpisode_User userRecord = null;
								if (dictEpUsers.ContainsKey(ep.AnimeEpisodeID))
									userRecord = dictEpUsers[ep.AnimeEpisodeID];

								Contract_AnimeEpisode epContract = ep.ToContract(anidbep, new List<VideoLocal>(), userRecord, serUserRec);
								candidateEps.Add(epContract);
							}
						}
					}

					if (candidateEps.Count > 0)
					{
						Dictionary<int, TvDB_Episode> dictTvDBEpisodes = anime.DictTvDBEpisodes;
						Dictionary<int, int> dictTvDBSeasons = anime.DictTvDBSeasons;
						Dictionary<int, int> dictTvDBSeasonsSpecials = anime.DictTvDBSeasonsSpecials;
						CrossRef_AniDB_TvDB tvDBCrossRef = anime.CrossRefTvDB;
						List<CrossRef_AniDB_TvDB_Episode> tvDBCrossRefEpisodes = anime.CrossRefTvDBEpisodes;
						Dictionary<int, int> dictTvDBCrossRefEpisodes = new Dictionary<int, int>();
						foreach (CrossRef_AniDB_TvDB_Episode xrefEp in tvDBCrossRefEpisodes)
							dictTvDBCrossRefEpisodes[xrefEp.AniDBEpisodeID] = xrefEp.TvDBEpisodeID;

						

						// sort by episode type and number to find the next episode
						List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
						sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeType", false, SortType.eInteger));
						sortCriteria.Add(new SortPropOrFieldAndDirection("EpisodeNumber", false, SortType.eInteger));
						candidateEps = Sorting.MultiSort<Contract_AnimeEpisode>(candidateEps, sortCriteria);

						// this will generate a lot of queries when the user doesn have files
						// for these episodes
						int cnt = 0;
						foreach (Contract_AnimeEpisode canEp in candidateEps)
						{
							
							if (dictAniEps.ContainsKey(canEp.AniDB_EpisodeID))
							{
								AniDB_Episode anidbep = dictAniEps[canEp.AniDB_EpisodeID];

								AnimeEpisode_User userEpRecord = null;
								if (dictEpUsers.ContainsKey(canEp.AnimeEpisodeID))
									userEpRecord = dictEpUsers[canEp.AnimeEpisodeID];

								// now refresh from the database to get file count
								AnimeEpisode epFresh = repEps.GetByID(canEp.AnimeEpisodeID);

								int fileCount = epFresh.VideoLocals.Count;
								if (fileCount > 0)
								{
									MetroContract_Anime_Episode contract = new MetroContract_Anime_Episode();
									contract.AnimeEpisodeID = epFresh.AnimeEpisodeID;
									contract.LocalFileCount = fileCount;

									if (userEpRecord == null)
										contract.IsWatched = false;
									else
										contract.IsWatched = userEpRecord.WatchedCount > 0;

									// anidb
									contract.EpisodeNumber = anidbep.EpisodeNumber;
									contract.EpisodeName = anidbep.RomajiName;
									if (!string.IsNullOrEmpty(anidbep.EnglishName))
										contract.EpisodeName = anidbep.EnglishName;

									contract.EpisodeType = anidbep.EpisodeType;
									contract.LengthSeconds = anidbep.LengthSeconds;
									contract.AirDate = anidbep.AirDateFormatted;

									// tvdb
									SetTvDBInfo(dictTvDBEpisodes, dictTvDBSeasons, dictTvDBSeasonsSpecials, tvDBCrossRef, dictTvDBCrossRefEpisodes, anidbep, ref contract);
									/*contract.EpisodeOverview = "";
									contract.ImageType = "";
									contract.ImageID = "";*/

									ret.NextEpisodesToWatch.Add(contract);
									cnt++;
								}
							}
							if (cnt == maxEpisodeRecords) break;
						}
					}
				}

				return ret;

			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return null;
			}
		}

		public MetroContract_Anime_Summary GetAnimeSummary(int animeID)
		{
			try
			{
				AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();

				AniDB_Anime anime = repAnime.GetByAnimeID(animeID);
				if (anime == null) return null;

				AnimeSeries ser = repSeries.GetByAnimeID(animeID);

				MetroContract_Anime_Summary summ = new MetroContract_Anime_Summary();
				summ.AnimeID = anime.AnimeID;
				summ.AnimeName = anime.MainTitle;
				summ.AnimeSeriesID = 0;

				summ.BeginYear = anime.BeginYear;
				summ.EndYear = anime.EndYear;
				summ.PosterName = anime.DefaultPosterPathNoBlanks;

				ImageDetails imgDet = anime.DefaultPosterDetailsNoBlanks;
				summ.ImageType = (int)imgDet.ImageType;
				summ.ImageID = imgDet.ImageID;

				if (ser != null)
				{
					summ.AnimeName = ser.SeriesName;
					summ.AnimeSeriesID = ser.AnimeSeriesID;
				}

				return summ;

			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}

			return null;
		}

		private void SetTvDBInfo(AniDB_Anime anime, AniDB_Episode ep, ref MetroContract_Anime_Episode contract)
		{
			Dictionary<int, TvDB_Episode> dictTvDBEpisodes = anime.DictTvDBEpisodes;
			Dictionary<int, int> dictTvDBSeasons = anime.DictTvDBSeasons;
			Dictionary<int, int> dictTvDBSeasonsSpecials = anime.DictTvDBSeasonsSpecials;
			CrossRef_AniDB_TvDB tvDBCrossRef = anime.CrossRefTvDB;
			List<CrossRef_AniDB_TvDB_Episode> tvDBCrossRefEpisodes = anime.CrossRefTvDBEpisodes;
			Dictionary<int, int> dictTvDBCrossRefEpisodes = new Dictionary<int, int>();
			foreach (CrossRef_AniDB_TvDB_Episode xrefEp in tvDBCrossRefEpisodes)
				dictTvDBCrossRefEpisodes[xrefEp.AniDBEpisodeID] = xrefEp.TvDBEpisodeID;

			SetTvDBInfo(dictTvDBEpisodes, dictTvDBSeasons, dictTvDBSeasonsSpecials, tvDBCrossRef, dictTvDBCrossRefEpisodes, ep, ref contract);
		}

		public void SetTvDBInfo(Dictionary<int, TvDB_Episode> dictTvDBEpisodes, Dictionary<int, int> dictTvDBSeasons,
			Dictionary<int, int> dictTvDBSeasonsSpecials, CrossRef_AniDB_TvDB tvDBCrossRef, Dictionary<int, int> dictTvDBCrossRefEpisodes,
			AniDB_Episode ep, ref MetroContract_Anime_Episode contract)
		{
			// check if this episode has a direct tvdb over-ride
			if (dictTvDBCrossRefEpisodes.ContainsKey(ep.EpisodeID))
			{
				foreach (TvDB_Episode tvep in dictTvDBEpisodes.Values)
				{
					if (dictTvDBCrossRefEpisodes[ep.EpisodeID] == tvep.Id)
					{
						if (string.IsNullOrEmpty(tvep.Overview))
							contract.EpisodeOverview = "Episode Overview Not Available";
						else
							contract.EpisodeOverview = tvep.Overview;

						if (string.IsNullOrEmpty(tvep.FullImagePath) || !File.Exists(tvep.FullImagePath))
						{
							contract.ImageType = 0;
							contract.ImageID = 0;
						}
						else
						{
							contract.ImageType = (int)JMMImageType.TvDB_Episode;
							contract.ImageID = tvep.TvDB_EpisodeID;
						}

						if (ServerSettings.EpisodeTitleSource == DataSourceType.TheTvDB && !string.IsNullOrEmpty(tvep.EpisodeName))
							contract.EpisodeName = tvep.EpisodeName;

						return;
					}
				}
			}

			// now do stuff to improve performance
			if (ep.EpisodeTypeEnum == enEpisodeType.Episode)
			{
				if (dictTvDBEpisodes != null && dictTvDBSeasons != null && tvDBCrossRef != null)
				{
					if (dictTvDBSeasons.ContainsKey(tvDBCrossRef.TvDBSeasonNumber))
					{
						int episodeNumber = dictTvDBSeasons[tvDBCrossRef.TvDBSeasonNumber] + ep.EpisodeNumber - 1;
						if (dictTvDBEpisodes.ContainsKey(episodeNumber))
						{

							TvDB_Episode tvep = dictTvDBEpisodes[episodeNumber];
							if (string.IsNullOrEmpty(tvep.Overview))
								contract.EpisodeOverview = "Episode Overview Not Available";
							else
								contract.EpisodeOverview = tvep.Overview;

							if (string.IsNullOrEmpty(tvep.FullImagePath) || !File.Exists(tvep.FullImagePath))
							{
								contract.ImageType = 0;
								contract.ImageID = 0;
							}
							else
							{
								contract.ImageType = (int)JMMImageType.TvDB_Episode;
								contract.ImageID = tvep.TvDB_EpisodeID;
							}

							if (ServerSettings.EpisodeTitleSource == DataSourceType.TheTvDB && !string.IsNullOrEmpty(tvep.EpisodeName))
								contract.EpisodeName = tvep.EpisodeName;
						}
					}
				}
			}

			if (ep.EpisodeTypeEnum == enEpisodeType.Special)
			{
				if (dictTvDBEpisodes != null && dictTvDBSeasonsSpecials != null && tvDBCrossRef != null)
				{
					if (dictTvDBSeasonsSpecials.ContainsKey(tvDBCrossRef.TvDBSeasonNumber))
					{
						int episodeNumber = dictTvDBSeasonsSpecials[tvDBCrossRef.TvDBSeasonNumber] + ep.EpisodeNumber - 1;
						if (dictTvDBEpisodes.ContainsKey(episodeNumber))
						{
							TvDB_Episode tvep = dictTvDBEpisodes[episodeNumber];
							contract.EpisodeOverview = tvep.Overview;

							if (string.IsNullOrEmpty(tvep.FullImagePath) || !File.Exists(tvep.FullImagePath))
							{
								contract.ImageType = 0;
								contract.ImageID = 0;
							}
							else
							{
								contract.ImageType = (int)JMMImageType.TvDB_Episode;
								contract.ImageID = tvep.TvDB_EpisodeID;
							}

							if (ServerSettings.EpisodeTitleSource == DataSourceType.TheTvDB && !string.IsNullOrEmpty(tvep.EpisodeName))
								contract.EpisodeName = tvep.EpisodeName;
						}
					}
				}
			}


		}

		public List<MetroContract_AniDB_Character> GetCharactersForAnime(int animeID, int maxRecords)
		{
			List<MetroContract_AniDB_Character> chars = new List<MetroContract_AniDB_Character>();

			try
			{
				AniDB_Anime_CharacterRepository repAnimeChar = new AniDB_Anime_CharacterRepository();
				AniDB_CharacterRepository repChar = new AniDB_CharacterRepository();

				List<AniDB_Anime_Character> animeChars = repAnimeChar.GetByAnimeID(animeID);
				if (animeChars == null || animeChars.Count == 0) return chars;

				int cnt = 0;

				// first get all the main characters
				foreach (AniDB_Anime_Character animeChar in animeChars.Where(item => item.CharType.Equals("main character in", StringComparison.InvariantCultureIgnoreCase)))
				{
					cnt++;
					AniDB_Character chr = repChar.GetByCharID(animeChar.CharID);
					if (chr != null)
					{
						MetroContract_AniDB_Character contract = new MetroContract_AniDB_Character();
						chars.Add(chr.ToContractMetro(animeChar));
					}

					if (cnt == maxRecords) break;
				}

				// now get the rest
				foreach (AniDB_Anime_Character animeChar in animeChars.Where(item => !item.CharType.Equals("main character in", StringComparison.InvariantCultureIgnoreCase)))
				{
					cnt++;
					AniDB_Character chr = repChar.GetByCharID(animeChar.CharID);
					if (chr != null)
					{
						MetroContract_AniDB_Character contract = new MetroContract_AniDB_Character();
						chars.Add(chr.ToContractMetro(animeChar));
					}

					if (cnt == maxRecords) break;
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
			return chars;
		}

		public List<MetroContract_Shout> GetTraktShoutsForAnime(int animeID, int maxRecords)
		{
			List<MetroContract_Shout> shouts = new List<MetroContract_Shout>();

			try
			{
				Trakt_FriendRepository repFriends = new Trakt_FriendRepository();

				List<TraktTV_ShoutGet> shoutsTemp = TraktTVHelper.GetShowShouts(animeID);
				if (shoutsTemp == null || shoutsTemp.Count == 0) return shouts;

				int cnt = 0;
				foreach (TraktTV_ShoutGet sht in shoutsTemp)
				{
					MetroContract_Shout shout = new MetroContract_Shout();

					Trakt_Friend traktFriend = repFriends.GetByUsername(sht.user.username);

					// user details
					Contract_Trakt_User user = new Contract_Trakt_User();
					if (traktFriend == null)
						shout.UserID = 0;
					else
						shout.UserID = traktFriend.Trakt_FriendID;

					shout.UserName = sht.user.username;

					// shout details
					shout.ShoutText = sht.shout;
					shout.IsSpoiler = sht.spoiler;
					shout.ShoutDate = Utils.GetAniDBDateAsDate(sht.inserted);

					shout.ImageURL = sht.user.avatar;
					shout.ShoutType = (int)WhatPeopleAreSayingType.TraktShout;
					shout.Source = "Trakt";

					cnt++;
					shouts.Add(shout);

					if (cnt == maxRecords) break;
				}

				if (shouts.Count > 0)
				{
					List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
					sortCriteria.Add(new SortPropOrFieldAndDirection("ShoutDate", false, SortType.eDateTime));
					shouts = Sorting.MultiSort<MetroContract_Shout>(shouts, sortCriteria);
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
			return shouts;
		}

		public List<MetroContract_Shout> GetAniDBRecommendationsForAnime(int animeID, int maxRecords)
		{
			List<MetroContract_Shout> contracts = new List<MetroContract_Shout>();
			try
			{

				AniDB_RecommendationRepository repBA = new AniDB_RecommendationRepository();

				int cnt = 0;
				foreach (AniDB_Recommendation rec in repBA.GetByAnimeID(animeID))
				{
					MetroContract_Shout shout = new MetroContract_Shout();

					shout.UserID = rec.UserID;
					shout.UserName = "";

					// shout details
					shout.ShoutText = rec.RecommendationText;
					shout.IsSpoiler = false;
					shout.ShoutDate = null;

					shout.ImageURL = string.Empty;

					AniDBRecommendationType recType = (AniDBRecommendationType)rec.RecommendationType;
					switch (recType)
					{
						case AniDBRecommendationType.ForFans: shout.ShoutType = (int)WhatPeopleAreSayingType.AniDBForFans; break;
						case AniDBRecommendationType.MustSee: shout.ShoutType = (int)WhatPeopleAreSayingType.AniDBMustSee; break;
						case AniDBRecommendationType.Recommended: shout.ShoutType = (int)WhatPeopleAreSayingType.AniDBRecommendation; break;
					}

					shout.Source = "AniDB";

					cnt++;
					contracts.Add(shout);

					if (cnt == maxRecords) break;
				}

				return contracts;
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return contracts;
			}
		}

		public List<MetroContract_Anime_Summary> GetSimilarAnimeForAnime(int animeID, int maxRecords, int jmmuserID)
		{
			List<Contract_AniDB_Anime_Similar> links = new List<Contract_AniDB_Anime_Similar>();
			List<MetroContract_Anime_Summary> retAnime = new List<MetroContract_Anime_Summary>();
			try
			{
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				AniDB_Anime anime = repAnime.GetByAnimeID(animeID);
				if (anime == null) return retAnime;

				JMMUserRepository repUsers = new JMMUserRepository();
				JMMUser juser = repUsers.GetByID(jmmuserID);
				if (juser == null) return retAnime;

				AnimeSeriesRepository repSeries = new AnimeSeriesRepository();

				foreach (AniDB_Anime_Similar link in anime.SimilarAnime)
				{
					AniDB_Anime animeLink = repAnime.GetByAnimeID(link.SimilarAnimeID);
					if (animeLink != null)
					{
						if (!juser.AllowedAnime(animeLink)) continue;
					}

					// check if this anime has a series
					AnimeSeries ser = repSeries.GetByAnimeID(link.SimilarAnimeID);

					MetroContract_Anime_Summary summ = new MetroContract_Anime_Summary();
					summ.AnimeID = animeLink.AnimeID;
					summ.AnimeName = animeLink.MainTitle;
					summ.AnimeSeriesID = 0;

					summ.BeginYear = animeLink.BeginYear;
					summ.EndYear = animeLink.EndYear;
					summ.PosterName = animeLink.DefaultPosterPathNoBlanks;

					ImageDetails imgDet = animeLink.DefaultPosterDetailsNoBlanks;
					summ.ImageType = (int)imgDet.ImageType;
					summ.ImageID = imgDet.ImageID;

					if (ser != null)
					{
						summ.AnimeName = ser.SeriesName;
						summ.AnimeSeriesID = ser.AnimeSeriesID;
					}
					
					retAnime.Add(summ);

					if (retAnime.Count == maxRecords) break;
				}

				return retAnime;
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return retAnime;
			}

		}
	}
}
