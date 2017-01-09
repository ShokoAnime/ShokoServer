using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using Shoko.Server.Utilities;
using NHibernate;
using Shoko.Server.Collections;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories
{
    /* NOTES
	 * Note 1 - I had a performance problem in sqlite where getting the video quality stats was taking a couple of minutes
	 *          Adding this extra unncessary join reduced that query down to less than a second
	 */

    public class AdhocRepository
    {
        private AdhocRepository()
        {
            
        }

        public static AdhocRepository Create()
        {
            return new AdhocRepository();
        }
        #region Video Quality

        /// <summary>
        /// Gets a list fo all the possible video quality settings for the user e.g. dvd, blu-ray
        /// </summary>
        /// <returns></returns>
        public List<string> GetAllVideoQuality()
        {
            List<string> allVidQuality = new List<string>();

            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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
        public Dictionary<int, HashSet<string>> GetAllVideoQualityByGroup()
        {
            Dictionary<int, HashSet<string>> allVidQuality = new Dictionary<int, HashSet<string>>();

            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                System.Data.IDbCommand command = session.Connection.CreateCommand();
                command.CommandText = "SELECT ag.AnimeGroupID, anifile.File_Source ";
                command.CommandText += "from AnimeGroup ag ";
                command.CommandText += "INNER JOIN AnimeSeries ser on ser.AnimeGroupID = ag.AnimeGroupID ";
                command.CommandText += "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID ";
                command.CommandText += "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID ";
                command.CommandText += "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID ";
                command.CommandText += "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash ";
                command.CommandText += "INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID ";
                // See Note 1
                command.CommandText += "GROUP BY ag.AnimeGroupID, anifile.File_Source ";


                using (IDataReader rdr = command.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        int groupID = int.Parse(rdr[0].ToString());
                        string vidQual = rdr[1].ToString().Trim();
                        HashSet<string> vids;
                        if (allVidQuality.ContainsKey(groupID))
                            vids = allVidQuality[groupID];
                        else
                        {
                            vids = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                            allVidQuality.Add(groupID, vids);
                        }
                        if (!vids.Contains(vidQual))
                            vids.Add(vidQual);
                    }
                }
            }

            return allVidQuality;
        }

        /// <summary>
        /// Get's all the video quality settings (comma separated) that apply to each group
        /// </summary>
        /// <returns></returns>
        public Dictionary<int, HashSet<string>> GetAllVideoQualityByAnime()
        {
            Dictionary<int, HashSet<string>> allVidQuality = new Dictionary<int, HashSet<string>>();

            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                System.Data.IDbCommand command = session.Connection.CreateCommand();
                command.CommandText = "SELECT anime.AnimeID, anime.MainTitle, anifile.File_Source ";
                command.CommandText += "FROM AnimeSeries ser ";
                command.CommandText += "INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID ";
                command.CommandText += "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID ";
                command.CommandText += "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID ";
                command.CommandText += "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID ";
                command.CommandText += "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash ";
                command.CommandText += "INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID ";
                // See Note 1
                command.CommandText += "GROUP BY anime.AnimeID, anime.MainTitle, anifile.File_Source ";


                using (IDataReader rdr = command.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        int groupID = int.Parse(rdr[0].ToString());
                        string vidQual = rdr[2].ToString().Trim();
                        HashSet<string> vids;
                        if (allVidQuality.ContainsKey(groupID))
                            vids = allVidQuality[groupID];
                        else
                        {
                            vids = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                            allVidQuality.Add(groupID, vids);
                        }
                        if (!vids.Contains(vidQual))
                            vids.Add(vidQual);
                    }
                }
            }

            return allVidQuality;
        }

        public HashSet<string> GetAllVideoQualityForGroup(int animeGroupID)
        {
            HashSet<string> vidQuals = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                System.Data.IDbCommand command = session.Connection.CreateCommand();
                command.CommandText = "SELECT anifile.File_Source ";
                command.CommandText += "from AnimeGroup ag ";
                command.CommandText += "INNER JOIN AnimeSeries ser on ser.AnimeGroupID = ag.AnimeGroupID ";
                command.CommandText += "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID ";
                command.CommandText += "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID ";
                command.CommandText += "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID ";
                command.CommandText += "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash ";
                command.CommandText += "INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID ";
                // See Note 1
                command.CommandText += "where ag.AnimeGroupID = " + animeGroupID.ToString();
                command.CommandText += " GROUP BY anifile.File_Source ";

                using (IDataReader rdr = command.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        string vidQual = rdr[0].ToString().Trim();
                        if (!vidQuals.Contains(vidQual))
                        {
                            vidQuals.Add(vidQual);
                        }
                    }
                }
                return vidQuals;
            }
        }

        /// <summary>
        /// Gets All video quality by group.
        /// </summary>
        /// <param name="session">The NHibernate session.</param>
        /// <param name="animeGroupIds">The optional list of group IDs to limit the results to.
        /// If <c>null</c> is specified, then results for ALL groups will be returned.</param>
        /// <returns>A <see cref="ILookup{TKey,TElement}"/> containing all video quality grouped by anime group ID.</returns>
        public ILookup<int, string> GetAllVideoQualityByGroup(ISessionWrapper session, IReadOnlyCollection<int> animeGroupIds = null)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (animeGroupIds != null && animeGroupIds.Count == 0)
            {
                return EmptyLookup<int, string>.Instance;
            }

            string query = @"
                SELECT DISTINCT ag.AnimeGroupID, anifile.File_Source
                    FROM AnimeGroup ag
                        INNER JOIN AnimeSeries ser
                            ON ser.AnimeGroupID = ag.AnimeGroupID
                        INNER JOIN AnimeEpisode ep
                            ON ep.AnimeSeriesID = ser.AnimeSeriesID
                        INNER JOIN AniDB_Episode aniep
                            ON ep.AniDB_EpisodeID = aniep.EpisodeID
                        INNER JOIN CrossRef_File_Episode xref
                            ON aniep.EpisodeID = xref.EpisodeID
                        INNER JOIN AniDB_File anifile
                            ON anifile.Hash = xref.Hash
                        INNER JOIN CrossRef_Subtitles_AniDB_File subt
                            ON subt.FileID = anifile.FileID";

            if (animeGroupIds != null)
            {
                query += @"
                    WHERE ag.AnimeGroupID IN (" + String.Join(",", animeGroupIds) + ")";
            }

            var results = session.CreateSQLQuery(query)
                .AddScalar("AnimeGroupID", NHibernateUtil.Int32)
                .AddScalar("File_Source", NHibernateUtil.String)
                .List<object[]>()
                .ToLookup(r => (int)r[0], r => (string)r[1]);

            return results;
        }

        public HashSet<string> GetAllVideoQualityForAnime(int animeID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetAllVideoQualityForAnime(session.Wrap(), animeID);
            }
        }

        public HashSet<string> GetAllVideoQualityForAnime(ISessionWrapper session, int animeID)
        {
            HashSet<string> vidQuals = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            System.Data.IDbCommand command = session.Connection.CreateCommand();
            command.CommandText = "SELECT anifile.File_Source "
               + "FROM AnimeSeries ser "
               + "INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID "
               + "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID "
               + "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID "
               + "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID "
               + "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash "
               + "INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID ";
            // See Note 1
            command.CommandText += "where anime.AnimeID = " + animeID.ToString()
                + " GROUP BY anifile.File_Source ";

            using (IDataReader rdr = command.ExecuteReader())
            {
                while (rdr.Read())
                {
                    string vidQual = rdr[0].ToString().Trim();
                    if (!vidQuals.Contains(vidQual))
                    {
                        vidQuals.Add(vidQual);
                    }
                }
            }
            return vidQuals;
        }

        public Dictionary<int, HashSet<string>> GetAllVideoQualityByAnime(ISessionWrapper session, ICollection<int> animeIDs)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (animeIDs == null)
                throw new ArgumentNullException(nameof(animeIDs));

            if (animeIDs.Count == 0)
            {
                return new Dictionary<int, HashSet<string>>();
            }

            using (IDbCommand command = session.Connection.CreateCommand())
            {
                command.CommandText = @"SELECT anime.AnimeID, anifile.File_Source
                    FROM AnimeSeries ser
                    INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID
                    INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID
                    INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID
                    INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID
                    INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash
                    INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID
                    WHERE anime.AnimeID IN (" + String.Join(",", animeIDs) + @")
                    GROUP BY anime.AnimeID, anifile.File_Source ";

                var allVidQualPerAnime = new Dictionary<int, HashSet<string>>();

                using (IDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int animeId = Convert.ToInt32(reader[0]);
                        string vidQual = reader[1].ToString().Trim();
                        HashSet<string> vidQualSet;

                        if (!allVidQualPerAnime.TryGetValue(animeId, out vidQualSet))
                        {
                            vidQualSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                            allVidQualPerAnime.Add(animeId, vidQualSet);
                        }

                        vidQualSet.Add(vidQual);
                    }
                }

                return allVidQualPerAnime;
            }
        }


        public Dictionary<int, AnimeVideoQualityStat> GetEpisodeVideoQualityStatsByAnime()
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetEpisodeVideoQualityStatsByAnime(session.Wrap());
            }
        }

        public Dictionary<int, AnimeVideoQualityStat> GetEpisodeVideoQualityStatsByAnime(ISessionWrapper session, IReadOnlyCollection<int> animeIds = null)
        {
            Dictionary<int, AnimeVideoQualityStat> dictStats = new Dictionary<int, AnimeVideoQualityStat>();

            using (IDbCommand command = session.Connection.CreateCommand())
            {
                command.CommandText = "SELECT anime.AnimeID, anime.MainTitle, anifile.File_Source, aniep.EpisodeNumber "
                    + "FROM AnimeSeries ser "
                    + "INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID "
                    + "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID "
                    + "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID "
                    + "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID "
                    + "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash "
                    + "INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID ";
                // See Note 1
                command.CommandText += "WHERE aniep.EpisodeType = 1 "; // normal episodes only

                if (animeIds != null)
                {
                    if (animeIds.Count == 0)
                    {
                        return dictStats; // No anime IDs means no results. So, no need to perform query
                    }

                    command.CommandText += "AND anime.AnimeID IN (" + String.Join(",", animeIds) + ") ";
                }

                command.CommandText += "GROUP BY anime.AnimeID, anime.MainTitle, anifile.File_Source, aniep.EpisodeNumber "
                    + "ORDER BY anime.AnimeID, anime.MainTitle, anifile.File_Source, aniep.EpisodeNumber ";

                using (IDataReader rdr = command.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        int animeID = Convert.ToInt32(rdr[0]);
                        string mainTitle = rdr[1].ToString().Trim();
                        string vidQual = rdr[2].ToString().Trim();
                        int epNumber = Convert.ToInt32(rdr[3]);
                        AnimeVideoQualityStat stat;

                        if (!dictStats.TryGetValue(animeID, out stat))
                        {
                            stat = new AnimeVideoQualityStat
                                {
                                    AnimeID = animeID,
                                    MainTitle = mainTitle,
                                    VideoQualityEpisodeCount = new Dictionary<string, int>()
                                };
                            dictStats.Add(animeID, stat);
                        }

                        int epCount;

                        stat.VideoQualityEpisodeCount.TryGetValue(vidQual, out epCount);
                        stat.VideoQualityEpisodeCount[vidQual] = epCount + 1;
                    }
                }
            }

            return dictStats;
        }

        public AnimeVideoQualityStat GetEpisodeVideoQualityStatsForAnime(ISessionWrapper session, int aID)
        {
            AnimeVideoQualityStat stat = new AnimeVideoQualityStat();
            stat.VideoQualityEpisodeCount = new Dictionary<string, int>();

            System.Data.IDbCommand command = session.Connection.CreateCommand();
            command.CommandText = "SELECT anime.AnimeID, anime.MainTitle, anifile.File_Source, aniep.EpisodeNumber "
                + "from AnimeSeries ser "
                + "INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID "
                + "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID "
                + "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID "
                + "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID "
                + "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash "
                + "INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID ";
            // See Note 1
            command.CommandText += "WHERE aniep.EpisodeType = 1 " // normal episodes only
                + "AND anime.AnimeID =  " + aID.ToString()
                + " GROUP BY anime.AnimeID, anime.MainTitle, anifile.File_Source, aniep.EpisodeNumber ";

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

            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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

            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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


            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                System.Data.IDbCommand command = session.Connection.CreateCommand();
                command.CommandText = "SELECT anime.AnimeID, anime.MainTitle, lan.LanguageName ";
                command.CommandText += "FROM AnimeSeries ser  ";
                command.CommandText += "INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID ";
                command.CommandText += "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID ";
                command.CommandText += "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID ";
                command.CommandText += "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID ";
                command.CommandText += "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash ";
                command.CommandText +=
                    "INNER JOIN CrossRef_Languages_AniDB_File audio on audio.FileID = anifile.FileID ";
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

        public Dictionary<int, LanguageStat> GetSubtitleLanguageStatsForAnime(ISessionWrapper session)
        {
            Dictionary<int, LanguageStat> dictStats = new Dictionary<int, LanguageStat>();
            const string query = "SELECT DISTINCT anime.AnimeID, anime.MainTitle, lan.LanguageName "
                + "FROM AnimeSeries ser  "
                + "INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID "
                + "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID "
                + "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID "
                + "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID "
                + "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash "
                + "INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID "
                + "INNER JOIN Language lan on subt.LanguageID = lan.LanguageID";

            var rows = session.CreateSQLQuery(query)
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .AddScalar("MainTitle", NHibernateUtil.String)
                .AddScalar("LanguageName", NHibernateUtil.String)
                .List<object[]>();

            foreach (object[] cols in rows)
            {
                int animeID = Convert.ToInt32(cols[0]);
                string mainTitle = cols[1].ToString().Trim();
                string lanName = cols[2].ToString().Trim();
                LanguageStat stat = null;

                if (!dictStats.TryGetValue(animeID, out stat))
                {
                    stat = new LanguageStat
                    {
                        AnimeID = animeID,
                        MainTitle = mainTitle,
                        LanguageNames = new List<string>()
                    };
                    dictStats.Add(animeID, stat);
                }

                stat.LanguageNames.Add(lanName);
            }

            return dictStats;
        }

        private string GetAudioLanguageStatsByAnimeSQL(string animeIdPredicate)
        {
            string sql = "SELECT DISTINCT anime.AnimeID, anime.MainTitle, lan.LanguageName "
               + "FROM AnimeSeries ser  "
               + "INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID "
               + "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID "
               + "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID "
               + "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID "
               + "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash "
               + "INNER JOIN CrossRef_Languages_AniDB_File audio on audio.FileID = anifile.FileID "
               + "INNER JOIN Language lan on audio.LanguageID = lan.LanguageID "
               + "WHERE anime.AnimeID " + animeIdPredicate;
            return sql;
        }

        private Dictionary<int, LanguageStat> GetAudioLanguageStatsByAnimeResults(ISessionWrapper session, string animeIdPredicate)
        {
            Dictionary<int, LanguageStat> dictStats = new Dictionary<int, LanguageStat>();
            string query = GetAudioLanguageStatsByAnimeSQL(animeIdPredicate);

            var rows = session.CreateSQLQuery(query)
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .AddScalar("MainTitle", NHibernateUtil.String)
                .AddScalar("LanguageName", NHibernateUtil.String)
                .List<object[]>();

            foreach (object[] cols in rows)
            {
                int animeID = Convert.ToInt32(cols[0]);
                string mainTitle = cols[1].ToString().Trim();
                string lanName = cols[2].ToString().Trim();
                LanguageStat stat = null;

                if (!dictStats.TryGetValue(animeID, out stat))
                {
                    stat = new LanguageStat
                    {
                        AnimeID = animeID,
                        MainTitle = mainTitle,
                        LanguageNames = new List<string>()
                    };
                    dictStats.Add(animeID, stat);
                }

                stat.LanguageNames.Add(lanName);
            }

            return dictStats;
        }


        public Dictionary<int, LanguageStat> GetAudioLanguageStatsByAnime(int aID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetAudioLanguageStatsByAnimeResults(session.Wrap(), " = " + aID);
            }
        }

        public Dictionary<int, LanguageStat> GetAudioLanguageStatsByAnime(ISessionWrapper session, int aID)
        {
            return GetAudioLanguageStatsByAnimeResults(session, " = " +aID);
        }

        public Dictionary<int, LanguageStat> GetAudioLanguageStatsByAnime(ISessionWrapper session, ICollection<int> aIDs)
        {
            if (aIDs.Count == 0)
            {
                return new Dictionary<int, LanguageStat>();
            }

            string predicate = " IN (" + String.Join(",", aIDs) + ") ";

            return GetAudioLanguageStatsByAnimeResults(session, predicate);
        }

        public Dictionary<int, LanguageStat> GetSubtitleLanguageStatsByAnime(int aID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetSubtitleLanguageStatsByAnimeResults(session.Wrap(), " = " + aID);
            }
        }

        public Dictionary<int, LanguageStat> GetSubtitleLanguageStatsByAnime(ISessionWrapper session, int aID)
        {
            return GetSubtitleLanguageStatsByAnimeResults(session, " = " + aID);
        }

        public Dictionary<int, LanguageStat> GetSubtitleLanguageStatsByAnime(ISessionWrapper session, ICollection<int> aIDs)
        {
            if (aIDs.Count == 0)
            {
                return new Dictionary<int, LanguageStat>();
            }

            string predicate = " IN (" + String.Join(",", aIDs) + ") ";

            return GetSubtitleLanguageStatsByAnimeResults(session, predicate);
        }

        private Dictionary<int, LanguageStat> GetSubtitleLanguageStatsByAnimeResults(ISessionWrapper session, string animeIdPredicate)
        {
            Dictionary<int, LanguageStat> dictStats = new Dictionary<int, LanguageStat>();
            string query = "SELECT DISTINCT anime.AnimeID, anime.MainTitle, lan.LanguageName "
                + "FROM AnimeSeries ser  "
                + "INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID "
                + "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID "
                + "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID "
                + "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID "
                + "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash "
                + "INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID "
                + "INNER JOIN Language lan on subt.LanguageID = lan.LanguageID "
                + "WHERE anime.AnimeID " + animeIdPredicate;

            var rows = session.CreateSQLQuery(query)
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .AddScalar("MainTitle", NHibernateUtil.String)
                .AddScalar("LanguageName", NHibernateUtil.String)
                .List<object[]>();

            foreach (object[] cols in rows)
            {
                int animeID = Convert.ToInt32(cols[0]);
                string mainTitle = cols[1].ToString().Trim();
                string lanName = cols[2].ToString().Trim();
                LanguageStat stat = null;

                if (!dictStats.TryGetValue(animeID, out stat))
                {
                    stat = new LanguageStat
                    {
                        AnimeID = animeID,
                        MainTitle = mainTitle,
                        LanguageNames = new List<string>()
                    };
                    dictStats.Add(animeID, stat);
                }

                stat.LanguageNames.Add(lanName);
            }

            return dictStats;
        }

        #endregion
    }

    public class AnimeVideoQualityStat
    {
        public int AnimeID { get; set; }
        public string MainTitle { get; set; }
        public Dictionary<string, int> VideoQualityEpisodeCount { get; set; }
        // video quality / number of episodes that match that quality
    }

    public class LanguageStat
    {
        public int AnimeID { get; set; }
        public string MainTitle { get; set; }
        public List<string> LanguageNames { get; set; } // a list of all the languages that apply to this anime
    }
}