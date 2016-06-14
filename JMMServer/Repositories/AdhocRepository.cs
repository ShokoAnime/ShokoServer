using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using System.Data;
using System.Diagnostics;
using NHibernate;

namespace JMMServer.Repositories
{
	/* NOTES
	 * Note 1 - I had a performance problem in sqlite where getting the video quality stats was taking a couple of minutes
	 *          Adding this extra unncessary join reduced that query down to less than a second
	 */
	public class AdhocRepository
	{
		#region Video Quality
		/// <summary>
		/// Gets a list fo all the possible video quality settings for the user e.g. dvd, blu-ray
		/// </summary>
		/// <returns></returns>
		public List<string> GetAllVideoQuality()
		{
			List<string> allVidQuality = new List<string>();

			using (var session = JMMService.SessionFactory.OpenSession())
			{
				System.Data.IDbCommand command = session.Connection.CreateCommand();
				command.CommandText = "SELECT Distinct(File_Source) FROM AniDB_File";

				using (IDataReader rdr = command.ExecuteReader())
				{
					while (rdr.Read())
					{
						string vidQual = rdr[0].ToString().Trim();
						allVidQuality.Add(vidQual);
					}
				}
			}

			return allVidQuality;
		}

		/// <summary>
		/// Get's all the video quality settings (comma separated) that apply to each group
		/// </summary>
		/// <returns></returns>
		public Dictionary<int, string> GetAllVideoQualityByGroup()
		{
			Dictionary<int, string> allVidQuality = new Dictionary<int, string>();

			using (var session = JMMService.SessionFactory.OpenSession())
			{
				System.Data.IDbCommand command = session.Connection.CreateCommand();
				command.CommandText = "SELECT ag.AnimeGroupID, anifile.File_Source ";
				command.CommandText += "from AnimeGroup ag ";
				command.CommandText += "INNER JOIN AnimeSeries ser on ser.AnimeGroupID = ag.AnimeGroupID ";
				command.CommandText += "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID ";
				command.CommandText += "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID ";
				command.CommandText += "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID ";
				command.CommandText += "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash ";
				command.CommandText += "INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID "; // See Note 1
				command.CommandText += "GROUP BY ag.AnimeGroupID, anifile.File_Source ";

				

				using (IDataReader rdr = command.ExecuteReader())
				{
					while (rdr.Read())
					{
						int groupID = int.Parse(rdr[0].ToString());
						string vidQual = rdr[1].ToString().Trim();

						if (!allVidQuality.ContainsKey(groupID))
							allVidQuality[groupID] = "";

						if (allVidQuality[groupID].Length > 0)
							allVidQuality[groupID] += ",";

						allVidQuality[groupID] += vidQual;
					}
				}
			}

			return allVidQuality;
		}

		/// <summary>
		/// Get's all the video quality settings (comma separated) that apply to each group
		/// </summary>
		/// <returns></returns>
		public Dictionary<int, string> GetAllVideoQualityByAnime()
		{
			Dictionary<int, string> allVidQuality = new Dictionary<int, string>();

			using (var session = JMMService.SessionFactory.OpenSession())
			{
				System.Data.IDbCommand command = session.Connection.CreateCommand();
				command.CommandText = "SELECT anime.AnimeID, anime.MainTitle, anifile.File_Source ";
				command.CommandText += "FROM AnimeSeries ser ";
				command.CommandText += "INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID ";
				command.CommandText += "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID ";
				command.CommandText += "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID ";
				command.CommandText += "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID ";
				command.CommandText += "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash ";
				command.CommandText += "INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID "; // See Note 1
				command.CommandText += "GROUP BY anime.AnimeID, anime.MainTitle, anifile.File_Source ";



				using (IDataReader rdr = command.ExecuteReader())
				{
					while (rdr.Read())
					{
						int groupID = int.Parse(rdr[0].ToString());
						string vidQual = rdr[2].ToString().Trim();

						if (!allVidQuality.ContainsKey(groupID))
							allVidQuality[groupID] = "";

						if (allVidQuality[groupID].Length > 0)
							allVidQuality[groupID] += ",";

						allVidQuality[groupID] += vidQual;
					}
				}
			}

			return allVidQuality;
		}

		public string GetAllVideoQualityForGroup(int animeGroupID)
		{
			string vidQuals = "";

			using (var session = JMMService.SessionFactory.OpenSession())
			{
				System.Data.IDbCommand command = session.Connection.CreateCommand();
				command.CommandText = "SELECT anifile.File_Source ";
				command.CommandText += "from AnimeGroup ag ";
				command.CommandText += "INNER JOIN AnimeSeries ser on ser.AnimeGroupID = ag.AnimeGroupID ";
				command.CommandText += "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID ";
				command.CommandText += "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID ";
				command.CommandText += "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID ";
				command.CommandText += "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash ";
				command.CommandText += "INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID "; // See Note 1
				command.CommandText += "where ag.AnimeGroupID = " + animeGroupID.ToString();
				command.CommandText += " GROUP BY anifile.File_Source ";

				using (IDataReader rdr = command.ExecuteReader())
				{
					while (rdr.Read())
					{
						string vidQual = rdr[0].ToString().Trim();

						if (vidQuals.Length > 0)
							vidQuals += ",";

						vidQuals += vidQual;
					}
				}
			}

			return vidQuals;
		}

		public string GetAllVideoQualityForAnime(int animeID)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetAllVideoQualityForAnime(session, animeID);
			}
		}

		public string GetAllVideoQualityForAnime(ISession session, int animeID)
		{
			string vidQuals = "";

			System.Data.IDbCommand command = session.Connection.CreateCommand();
			command.CommandText = "SELECT anifile.File_Source ";
			command.CommandText += "FROM AnimeSeries ser ";
			command.CommandText += "INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID ";
			command.CommandText += "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID ";
			command.CommandText += "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID ";
			command.CommandText += "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID ";
			command.CommandText += "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash ";
			command.CommandText += "INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID "; // See Note 1
			command.CommandText += "where anime.AnimeID = " + animeID.ToString();
			command.CommandText += " GROUP BY anifile.File_Source ";

			using (IDataReader rdr = command.ExecuteReader())
			{
				while (rdr.Read())
				{
					string vidQual = rdr[0].ToString().Trim();

					if (vidQuals.Length > 0)
						vidQuals += ",";

					vidQuals += vidQual;
				}
			}

			return vidQuals;
		}


		public Dictionary<int, AnimeVideoQualityStat> GetEpisodeVideoQualityStatsByAnime()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetEpisodeVideoQualityStatsByAnime(session);
			}
		}

		public Dictionary<int, AnimeVideoQualityStat> GetEpisodeVideoQualityStatsByAnime(ISession session)
		{
			Dictionary<int, AnimeVideoQualityStat> dictStats = new Dictionary<int, AnimeVideoQualityStat>();
			
			System.Data.IDbCommand command = session.Connection.CreateCommand();
			command.CommandText = "SELECT anime.AnimeID, anime.MainTitle, anifile.File_Source, aniep.EpisodeNumber ";
			command.CommandText += "from AnimeSeries ser ";
			command.CommandText += "INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID ";
			command.CommandText += "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID ";
			command.CommandText += "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID ";
			command.CommandText += "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID ";
			command.CommandText += "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash ";
			command.CommandText += "INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID "; // See Note 1
			command.CommandText += "WHERE aniep.EpisodeType = 1 "; // normal episodes only
			command.CommandText += "GROUP BY anime.AnimeID, anime.MainTitle, anifile.File_Source, aniep.EpisodeNumber ";
			command.CommandText += "ORDER BY anime.AnimeID, anime.MainTitle, anifile.File_Source, aniep.EpisodeNumber ";

			using (IDataReader rdr = command.ExecuteReader())
			{
				while (rdr.Read())
				{
					int animeID = int.Parse(rdr[0].ToString());
					string mainTitle = rdr[1].ToString().Trim();
					string vidQual = rdr[2].ToString().Trim();
					int epNumber = int.Parse(rdr[3].ToString());

					if (animeID == 7656)
					{
						Debug.Print("");
					}

					if (!dictStats.ContainsKey(animeID))
					{
						AnimeVideoQualityStat stat = new AnimeVideoQualityStat();
						stat.AnimeID = animeID;
						stat.MainTitle = mainTitle;
						stat.VideoQualityEpisodeCount = new Dictionary<string, int>();
						stat.VideoQualityEpisodeCount[vidQual] = 1;
						dictStats[animeID] = stat;
					}
					else
					{
						if (dictStats[animeID].VideoQualityEpisodeCount.ContainsKey(vidQual))
							dictStats[animeID].VideoQualityEpisodeCount[vidQual]++;
						else
							dictStats[animeID].VideoQualityEpisodeCount[vidQual] = 1;
					}
				}
			}

			return dictStats;
		}

		public AnimeVideoQualityStat GetEpisodeVideoQualityStatsForAnime(ISession session, int aID)
		{
			AnimeVideoQualityStat stat = new AnimeVideoQualityStat();
			stat.VideoQualityEpisodeCount = new Dictionary<string, int>();

			System.Data.IDbCommand command = session.Connection.CreateCommand();
			command.CommandText = "SELECT anime.AnimeID, anime.MainTitle, anifile.File_Source, aniep.EpisodeNumber ";
			command.CommandText += "from AnimeSeries ser ";
			command.CommandText += "INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID ";
			command.CommandText += "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID ";
			command.CommandText += "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID ";
			command.CommandText += "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID ";
			command.CommandText += "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash ";
			command.CommandText += "INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID "; // See Note 1
			command.CommandText += "WHERE aniep.EpisodeType = 1 "; // normal episodes only
			command.CommandText += "AND anime.AnimeID =  " + aID.ToString();
			command.CommandText += " GROUP BY anime.AnimeID, anime.MainTitle, anifile.File_Source, aniep.EpisodeNumber ";

			using (IDataReader rdr = command.ExecuteReader())
			{
				while (rdr.Read())
				{
					stat.AnimeID = int.Parse(rdr[0].ToString());
					stat.MainTitle = rdr[1].ToString().Trim();

					string vidQual = rdr[2].ToString().Trim();
					int epNumber = int.Parse(rdr[3].ToString());

					if (!stat.VideoQualityEpisodeCount.ContainsKey(vidQual))
						stat.VideoQualityEpisodeCount[vidQual] = 1;
					else
						stat.VideoQualityEpisodeCount[vidQual]++;
				}
			}

			return stat;
		}

		#endregion

		#region Release Groups
		/// <summary>
		/// Gets a list of all the possible release groups for the user e.g. doki, chihiro
		/// </summary>
		/// <returns></returns>
		public List<string> GetAllReleaseGroups()
		{
			List<string> allReleaseGroups = new List<string>();

			/*
			-- Release groups by anime group
			SELECT ag.AnimeGroupID, ag.GroupName, anifile.Anime_GroupName
			from AnimeGroup ag
			INNER JOIN AnimeSeries ser on ser.AnimeGroupID = ag.AnimeGroupID
			INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID
			INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID
			INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID
			INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash
			--where ag.AnimeGroupID = 127
			GROUP BY ag.AnimeGroupID, ag.GroupName, anifile.Anime_GroupName

			-- Unique release groups
			SELECT anifile.Anime_GroupName, anifile.Anime_GroupNameShort
			from AniDB_Episode aniep
			INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID
			INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash
			GROUP BY anifile.Anime_GroupName, anifile.Anime_GroupNameShort
			*/

			return allReleaseGroups;
		}

		#endregion

		#region Audio and Subtitle Languages
		/// <summary>
		/// Gets a list of all the possible audio languages
		/// </summary>
		/// <returns></returns>
		public List<string> GetAllUniqueAudioLanguages()
		{
			List<string> allLanguages = new List<string>();

			using (var session = JMMService.SessionFactory.OpenSession())
			{
				System.Data.IDbCommand command = session.Connection.CreateCommand();
				command.CommandText = "SELECT Distinct(lan.LanguageName) ";
				command.CommandText += "FROM CrossRef_Languages_AniDB_File audio ";
				command.CommandText += "INNER JOIN Language lan on audio.LanguageID = lan.LanguageID ";
				command.CommandText += "ORDER BY lan.LanguageName ";

				using (IDataReader rdr = command.ExecuteReader())
				{
					while (rdr.Read())
					{
						string lan = rdr[0].ToString().Trim();
						allLanguages.Add(lan);
					}
				}
			}

			return allLanguages;
		}

		/// <summary>
		/// Gets a list of all the possible subtitle languages
		/// </summary>
		/// <returns></returns>
		public List<string> GetAllUniqueSubtitleLanguages()
		{
			List<string> allLanguages = new List<string>();

			using (var session = JMMService.SessionFactory.OpenSession())
			{
				System.Data.IDbCommand command = session.Connection.CreateCommand();
				command.CommandText = "SELECT Distinct(lan.LanguageName) ";
				command.CommandText += "FROM CrossRef_Subtitles_AniDB_File subt ";
				command.CommandText += "INNER JOIN Language lan on subt.LanguageID = lan.LanguageID ";
				command.CommandText += "ORDER BY lan.LanguageName ";

				using (IDataReader rdr = command.ExecuteReader())
				{
					while (rdr.Read())
					{
						string lan = rdr[0].ToString().Trim();
						allLanguages.Add(lan);
					}
				}
			}

			return allLanguages;
		}

		public Dictionary<int, LanguageStat> GetAudioLanguageStatsForAnime()
		{
			Dictionary<int, LanguageStat> dictStats = new Dictionary<int, LanguageStat>();


			using (var session = JMMService.SessionFactory.OpenSession())
			{
				System.Data.IDbCommand command = session.Connection.CreateCommand();
				command.CommandText = "SELECT anime.AnimeID, anime.MainTitle, lan.LanguageName ";
				command.CommandText += "FROM AnimeSeries ser  ";
				command.CommandText += "INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID ";
				command.CommandText += "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID ";
				command.CommandText += "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID ";
				command.CommandText += "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID ";
				command.CommandText += "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash ";
				command.CommandText += "INNER JOIN CrossRef_Languages_AniDB_File audio on audio.FileID = anifile.FileID ";
				command.CommandText += "INNER JOIN Language lan on audio.LanguageID = lan.LanguageID ";
				command.CommandText += "GROUP BY anime.AnimeID, anime.MainTitle, lan.LanguageName ";

				using (IDataReader rdr = command.ExecuteReader())
				{
					while (rdr.Read())
					{
						int animeID = int.Parse(rdr[0].ToString());
						string mainTitle = rdr[1].ToString().Trim();
						string lanName = rdr[2].ToString().Trim();
						

						if (animeID == 7656)
						{
							Debug.Print("");
						}

						if (!dictStats.ContainsKey(animeID))
						{
							LanguageStat stat = new LanguageStat();
							stat.AnimeID = animeID;
							stat.MainTitle = mainTitle;
							stat.LanguageNames = new List<string>();
							stat.LanguageNames.Add(lanName);
							dictStats[animeID] = stat;
						}
						else
							dictStats[animeID].LanguageNames.Add(lanName);
						
					}
				}
			}

			return dictStats;
		}

		public Dictionary<int, LanguageStat> GetSubtitleLanguageStatsForAnime()
		{
			Dictionary<int, LanguageStat> dictStats = new Dictionary<int, LanguageStat>();


			using (var session = JMMService.SessionFactory.OpenSession())
			{
				System.Data.IDbCommand command = session.Connection.CreateCommand();
				command.CommandText = "SELECT anime.AnimeID, anime.MainTitle, lan.LanguageName ";
				command.CommandText += "FROM AnimeSeries ser  ";
				command.CommandText += "INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID ";
				command.CommandText += "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID ";
				command.CommandText += "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID ";
				command.CommandText += "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID ";
				command.CommandText += "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash ";
				command.CommandText += "INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID ";
				command.CommandText += "INNER JOIN Language lan on subt.LanguageID = lan.LanguageID ";
				command.CommandText += "GROUP BY anime.AnimeID, anime.MainTitle, lan.LanguageName ";

				using (IDataReader rdr = command.ExecuteReader())
				{
					while (rdr.Read())
					{
						int animeID = int.Parse(rdr[0].ToString());
						string mainTitle = rdr[1].ToString().Trim();
						string lanName = rdr[2].ToString().Trim();


						if (animeID == 7656)
						{
							Debug.Print("");
						}

						if (!dictStats.ContainsKey(animeID))
						{
							LanguageStat stat = new LanguageStat();
							stat.AnimeID = animeID;
							stat.MainTitle = mainTitle;
							stat.LanguageNames = new List<string>();
							stat.LanguageNames.Add(lanName);
							dictStats[animeID] = stat;
						}
						else
							dictStats[animeID].LanguageNames.Add(lanName);

					}
				}
			}

			return dictStats;
		}

		private string GetAudioLanguageStatsByAnimeSQL(int aID)
		{
			string sql = "SELECT anime.AnimeID, anime.MainTitle, lan.LanguageName ";
			sql += "FROM AnimeSeries ser  ";
			sql += "INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID ";
			sql += "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID ";
			sql += "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID ";
			sql += "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID ";
			sql += "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash ";
			sql += "INNER JOIN CrossRef_Languages_AniDB_File audio on audio.FileID = anifile.FileID ";
			sql += "INNER JOIN Language lan on audio.LanguageID = lan.LanguageID ";
			sql += "WHERE anime.AnimeID = " + aID.ToString();
			sql += " GROUP BY anime.AnimeID, anime.MainTitle, lan.LanguageName ";
			return sql;
		}

		private Dictionary<int, LanguageStat> GetAudioLanguageStatsByAnimeResults(ISession session, int aID)
		{
			Dictionary<int, LanguageStat> dictStats = new Dictionary<int, LanguageStat>();

			System.Data.IDbCommand command = session.Connection.CreateCommand();
			command.CommandText = GetAudioLanguageStatsByAnimeSQL(aID);

			using (IDataReader rdr = command.ExecuteReader())
			{
				while (rdr.Read())
				{
					int animeID = int.Parse(rdr[0].ToString());
					string mainTitle = rdr[1].ToString().Trim();
					string lanName = rdr[2].ToString().Trim();

					if (!dictStats.ContainsKey(animeID))
					{
						LanguageStat stat = new LanguageStat();
						stat.AnimeID = animeID;
						stat.MainTitle = mainTitle;
						stat.LanguageNames = new List<string>();
						stat.LanguageNames.Add(lanName);
						dictStats[animeID] = stat;
					}
					else
						dictStats[animeID].LanguageNames.Add(lanName);

				}
			}

			return dictStats;
		}


		public Dictionary<int, LanguageStat> GetAudioLanguageStatsByAnime(int aID)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetAudioLanguageStatsByAnimeResults(session, aID);
			}
		}

		public Dictionary<int, LanguageStat> GetAudioLanguageStatsByAnime(ISession session, int aID)
		{
			return GetAudioLanguageStatsByAnimeResults(session, aID);
		}

		public Dictionary<int, LanguageStat> GetSubtitleLanguageStatsByAnime(int aID)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetSubtitleLanguageStatsByAnimeResults(session, aID);
			}
		}

		public Dictionary<int, LanguageStat> GetSubtitleLanguageStatsByAnime(ISession session, int aID)
		{
			return GetSubtitleLanguageStatsByAnimeResults(session, aID);
		}

		public Dictionary<int, LanguageStat> GetSubtitleLanguageStatsByAnimeResults(ISession session, int aID)
		{
			Dictionary<int, LanguageStat> dictStats = new Dictionary<int, LanguageStat>();

			System.Data.IDbCommand command = session.Connection.CreateCommand();
			command.CommandText = "SELECT anime.AnimeID, anime.MainTitle, lan.LanguageName ";
			command.CommandText += "FROM AnimeSeries ser  ";
			command.CommandText += "INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID ";
			command.CommandText += "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID ";
			command.CommandText += "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID ";
			command.CommandText += "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID ";
			command.CommandText += "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash ";
			command.CommandText += "INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID ";
			command.CommandText += "INNER JOIN Language lan on subt.LanguageID = lan.LanguageID ";
			command.CommandText += "WHERE anime.AnimeID = " + aID.ToString();
			command.CommandText += " GROUP BY anime.AnimeID, anime.MainTitle, lan.LanguageName ";

			using (IDataReader rdr = command.ExecuteReader())
			{
				while (rdr.Read())
				{
					int animeID = int.Parse(rdr[0].ToString());
					string mainTitle = rdr[1].ToString().Trim();
					string lanName = rdr[2].ToString().Trim();

					if (!dictStats.ContainsKey(animeID))
					{
						LanguageStat stat = new LanguageStat();
						stat.AnimeID = animeID;
						stat.MainTitle = mainTitle;
						stat.LanguageNames = new List<string>();
						stat.LanguageNames.Add(lanName);
						dictStats[animeID] = stat;
					}
					else
						dictStats[animeID].LanguageNames.Add(lanName);

				}
			}

			return dictStats;
		}

		#endregion
	}

	public class AnimeVideoQualityStat
	{
		public int AnimeID { get; set; }
		public string MainTitle { get; set; }
		public Dictionary<string, int> VideoQualityEpisodeCount { get; set; } // video quality / number of episodes that match that quality

	}

	public class LanguageStat
	{
		public int AnimeID { get; set; }
		public string MainTitle { get; set; }
		public List<string> LanguageNames { get; set; } // a list of all the languages that apply to this anime

	}
}
