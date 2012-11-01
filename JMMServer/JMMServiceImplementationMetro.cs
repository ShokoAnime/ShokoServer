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

namespace JMMServer
{
	public class JMMServiceImplementationMetro : IJMMServerMetro
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

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

		public MetroContract_Anime_Detail GetAnimeDetail(int animeID)
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

				ret.Rating = 0;
				

				return ret;

			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return null;
			}
		}

		public List<MetroContract_AniDB_Character> GetCharactersForAnime(int animeID)
		{
			List<MetroContract_AniDB_Character> chars = new List<MetroContract_AniDB_Character>();

			try
			{
				AniDB_Anime_CharacterRepository repAnimeChar = new AniDB_Anime_CharacterRepository();
				AniDB_CharacterRepository repChar = new AniDB_CharacterRepository();

				List<AniDB_Anime_Character> animeChars = repAnimeChar.GetByAnimeID(animeID);
				if (animeChars == null || animeChars.Count == 0) return chars;

				foreach (AniDB_Anime_Character animeChar in animeChars)
				{
					AniDB_Character chr = repChar.GetByCharID(animeChar.CharID);
					if (chr != null)
					{
						MetroContract_AniDB_Character contract = new MetroContract_AniDB_Character();
						chars.Add(chr.ToContractMetro(animeChar));
					}
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
			return chars;
		}
	}
}
