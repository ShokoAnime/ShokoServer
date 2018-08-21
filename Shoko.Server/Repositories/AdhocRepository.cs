using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Utils;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories
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
            return Repo.AniDB_File.GetAllVideoQuality();
        }

        /// <summary>
        /// Get's all the video quality settings (comma separated) that apply to each group
        /// </summary>
        /// <returns></returns>
        public Dictionary<int, HashSet<string>> GetAllVideoQualityByGroup()
        {
            Dictionary<int, List<int>> groupsseries = Repo.AnimeSeries.GetGroupByAnimeGroupIDAnimeSeries();
            Dictionary<int, List<int>> series = Repo.AnimeEpisode.GetGroupByAnimeSeriesIDEpisodes();
            Dictionary<int, List<string>> ephashes = Repo.CrossRef_File_Episode.GetGroupByEpisodeIDHashes();
            Dictionary<string, List<string>> hashesfiles = Repo.AniDB_File.GetGroupByHashFileSource();
            return groupsseries.ToDictionary(a => a.Key,a => new HashSet<string>(a.Value.SelectMany(b => series[b])
                    .SelectMany(b => ephashes[b]).SelectMany(b => hashesfiles[b]).Distinct()));

            /*




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

            return allVidQuality;*/
        }

        /// <summary>
        /// Get's all the video quality settings (comma separated) that apply to each group
        /// </summary>
        /// <returns></returns>
        public Dictionary<int, HashSet<string>> GetAllVideoQualityByAnime()
        {
            Dictionary<int, List<int>> series = Repo.AniDB_Episode.GetGroupByAnimeIDEpisodes();
            Dictionary<int, List<string>> ephashes = Repo.CrossRef_File_Episode.GetGroupByEpisodeIDHashes();
            Dictionary<string, List<string>> hashesfiles = Repo.AniDB_File.GetGroupByHashFileSource();
            return series.ToDictionary(a => a.Key, a => new HashSet<string>(a.Value.SelectMany(b => ephashes[b]).SelectMany(b => hashesfiles[b]).Distinct()));


         /*

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

            return allVidQuality;*/
        }

        public HashSet<string> GetAllVideoQualityForGroup(int animeGroupID)
        {
            return new HashSet<string>(Repo.AniDB_File.GetFileSourcesFromHashes(
                Repo.CrossRef_File_Episode.GetHashesByEpisodeIds(
                    Repo.AnimeEpisode.GetAniDBEpisodesIdBySeriesIDs(
                        Repo.AnimeSeries.GetSeriesIdByGroupID(animeGroupID)))));


            /*
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
            */
        }

        /// <summary>
        /// Gets All video quality by group.
        /// </summary>
        /// <param name="animeGroupIds">The optional list of group IDs to limit the results to.
        /// If <c>null</c> is specified, then results for ALL groups will be returned.</param>
        /// <returns>A <see cref="ILookup{TKey,TElement}"/> containing all video quality grouped by anime group ID.</returns>
        public Dictionary<int, HashSet<string>> GetAllVideoQualityByGroup(
            IEnumerable<int> animeGroupIds)
        {
            return animeGroupIds.ToDictionary(a => a, GetAllVideoQualityForGroup);

            /*
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
                .ToLookup(r => (int) r[0], r => (string) r[1]);

            return results;
            */
        }



        public HashSet<string> GetAllVideoQualityForAnime(int animeID)
        {
            return new HashSet<string>(Repo.AniDB_File.GetFileSourcesFromHashes(
                Repo.CrossRef_File_Episode.GetHashesByEpisodeIds(
                    Repo.AniDB_Episode.GetAniDBEpisodesIdByAnimeId(animeID))));

            /*
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
            return vidQuals;*/
        }

        public Dictionary<int, HashSet<string>> GetAllVideoQualityByAnime(IEnumerable<int> animeIDs)
        {
            return animeIDs.ToDictionary(a => a, GetAllVideoQualityForAnime);
            /*
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

                        if (!allVidQualPerAnime.TryGetValue(animeId, out HashSet<string> vidQualSet))
                        {
                            vidQualSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                            allVidQualPerAnime.Add(animeId, vidQualSet);
                        }

                        vidQualSet.Add(vidQual);
                    }
                }

                return allVidQualPerAnime;
            }*/
        }


        public Dictionary<int, AnimeVideoQualityStat> GetEpisodeVideoQualityStatsByAnime(IEnumerable<int> animeIds)
        {
            return animeIds.ToDictionary(a => a, GetEpisodeVideoQualityStatsForAnime);
            /*
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
                                      +
                                      "INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID ";
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

                command.CommandText +=
                    "GROUP BY anime.AnimeID, anime.MainTitle, anifile.File_Source, aniep.EpisodeNumber "
                    + "ORDER BY anime.AnimeID, anime.MainTitle, anifile.File_Source, aniep.EpisodeNumber ";

                using (IDataReader rdr = command.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        int animeID = Convert.ToInt32(rdr[0]);
                        string mainTitle = rdr[1].ToString().Trim();
                        string vidQual = rdr[2].ToString().Trim();
                        int epNumber = Convert.ToInt32(rdr[3]);

                        if (!dictStats.TryGetValue(animeID, out AnimeVideoQualityStat stat))
                        {
                            stat = new AnimeVideoQualityStat
                            {
                                AnimeID = animeID,
                                MainTitle = mainTitle,
                                VideoQualityEpisodeCount = new Dictionary<string, int>()
                            };
                            dictStats.Add(animeID, stat);
                        }


                        stat.VideoQualityEpisodeCount.TryGetValue(vidQual, out int epCount);
                        stat.VideoQualityEpisodeCount[vidQual] = epCount + 1;
                    }
                }
            }

            return dictStats;*/
        }

        public AnimeVideoQualityStat GetEpisodeVideoQualityStatsForAnime(int aID)
        {
            AniDB_Anime anime = Repo.AniDB_Anime.GetByID(aID);
            if (anime != null)
            {
                Dictionary<string, int> sources =
                    Repo.AniDB_Episode.GetAniDBEpisodesIdByAnimeId(aID).SelectMany(a =>
                            Repo.AniDB_File.GetFileSourcesFromHashes(
                                    Repo.CrossRef_File_Episode.GetHashesByEpisodeId(a))
                                .Select(z => new {Source = z, Episode = a})).GroupBy(m => m.Source)
                        .ToDictionary(a => a.Key, a => a.Count());
                return new AnimeVideoQualityStat {  AnimeID = aID, MainTitle = anime.MainTitle, VideoQualityEpisodeCount = sources};
            }

            return null;
            /*
            AnimeVideoQualityStat stat = new AnimeVideoQualityStat
            {
                VideoQualityEpisodeCount = new Dictionary<string, int>()
            };
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
                                   +
                                   " GROUP BY anime.AnimeID, anime.MainTitle, anifile.File_Source, aniep.EpisodeNumber ";

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
            */
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
            return Repo.Language.GetAllUniqueAudioLanguages();
            /*
            List<string> allLanguages = new List<string>();

            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                System.Data.IDbCommand command = session.Connection.CreateCommand();
#pragma warning disable 2100
                command.CommandText = "SELECT Distinct(lan.LanguageName) ";
                command.CommandText += "FROM CrossRef_Languages_AniDB_File audio ";
                command.CommandText += "INNER JOIN Language lan on audio.LanguageID = lan.LanguageID ";
                command.CommandText += "ORDER BY lan.LanguageName ";
#pragma warning restore 2100

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
            */
        }

        /// <summary>
        /// Gets a list of all the possible subtitle languages
        /// </summary>
        /// <returns></returns>
        public List<string> GetAllUniqueSubtitleLanguages()
        {
            return Repo.Language.GetAllUniqueSubtitleLanguages();
            /*


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

            return allLanguages;*/
        }

        public LanguageStat GetAudioLanguageStatByAnime(int aID)
        {
            AniDB_Anime an = Repo.AniDB_Anime.GetByID(aID);
            if (an != null)
            {
                return new LanguageStat
                {
                    AnimeID = aID,
                    MainTitle = an.MainTitle,
                    LanguageNames = Repo.Language.GetMany(Repo.CrossRef_Languages_AniDB_File.GetIdsByFilesIDs(
                        Repo.AniDB_File.GetFileIdsFromHashes(
                            Repo.CrossRef_File_Episode.GetHashesByEpisodeIds(
                                Repo.AniDB_Episode.GetAniDBEpisodesIdByAnimeId(aID))))).Select(a => a.LanguageName).ToList()
                };
            }
            return null;
        }

        internal Dictionary<int, LanguageStat> GetAudioLanguageStatsByAnime(int animeId) => GetAudioLanguageStatsByAnime(new int[] { animeId });

        internal Dictionary<int, LanguageStat> GetAudioLanguageStatsByAnime(ICollection<int> animeIds)
        {
            if (animeIds.Count == 0)
            {
                return new Dictionary<int, LanguageStat>();
            }

            var rows = Repo.AnimeSeries.GetAll()
                .Join(Repo.AniDB_Anime.GetAll(), s => s.AniDB_ID, j => j.AnimeID, (ser, anime) => new { ser, anime })
                .Join(Repo.AnimeEpisode.GetAll(), s => s.ser.AnimeSeriesID, j => j.AnimeSeriesID, (cmb, ep) => new { cmb.ser, cmb.anime, ep })
                .Join(Repo.AniDB_Episode.GetAll(), s => s.ep.AniDB_EpisodeID, aniep => aniep.EpisodeID, (cmb, aniep) => new { cmb.ser, cmb.anime, cmb.ep, aniep })
                .Join(Repo.CrossRef_File_Episode.GetAll(), s => s.aniep.EpisodeID, xref => xref.EpisodeID, (cmb, xref) => new { cmb.ser, cmb.anime, cmb.ep, cmb.aniep, xref })
                .Join(Repo.AniDB_File.GetAll(), s => s.xref.Hash, anifile => anifile.Hash, (cmb, anifile) => new { cmb.ser, cmb.anime, cmb.ep, cmb.aniep, cmb.xref, anifile })
                .Join(Repo.CrossRef_Languages_AniDB_File.GetAll(), s => s.anifile.FileID, audio => audio.FileID, (cmb, audio) => new { cmb.ser, cmb.anime, cmb.ep, cmb.aniep, cmb.xref, cmb.anifile, audio })
                .Join(Repo.Language.GetAll(), s => s.audio.LanguageID, lan => lan.LanguageID, (cmb, lan) => new { cmb.ser, cmb.anime, cmb.ep, cmb.aniep, cmb.xref, cmb.anifile, cmb.audio, lan })
                .Where(s => animeIds.Contains(s.anime.AnimeID))
                .Select(s => (s.anime.AnimeID, s.anime.MainTitle, s.lan.LanguageName))
                .Distinct();

            Dictionary<int, LanguageStat> dictStats = new Dictionary<int, LanguageStat>();

            foreach ((int animeID, string mainTitle, string lanName) in rows)
            {
                if (!dictStats.TryGetValue(animeID, out LanguageStat stat))
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

        public Dictionary<int, LanguageStat> GetSubtitleLanguageStatsByAnime(int aID) => GetAudioLanguageStatsByAnime(new int[] { aID });

        public Dictionary<int, LanguageStat> GetSubtitleLanguageStatsByAnime(IEnumerable<int> animeIds)
        {
            if (!animeIds.Any())
            {
                return new Dictionary<int, LanguageStat>();
            }

            var rows = Repo.AnimeSeries.GetAll()
                .Join(Repo.AniDB_Anime.GetAll(), s => s.AniDB_ID, j => j.AnimeID, (ser, anime) => new { ser, anime })
                .Join(Repo.AnimeEpisode.GetAll(), s => s.ser.AnimeSeriesID, j => j.AnimeSeriesID, (cmb, ep) => new { cmb.ser, cmb.anime, ep })
                .Join(Repo.AniDB_Episode.GetAll(), s => s.ep.AniDB_EpisodeID, aniep => aniep.EpisodeID, (cmb, aniep) => new { cmb.ser, cmb.anime, cmb.ep, aniep })
                .Join(Repo.CrossRef_File_Episode.GetAll(), s => s.aniep.EpisodeID, xref => xref.EpisodeID, (cmb, xref) => new { cmb.ser, cmb.anime, cmb.ep, cmb.aniep, xref })
                .Join(Repo.AniDB_File.GetAll(), s => s.xref.Hash, anifile => anifile.Hash, (cmb, anifile) => new { cmb.ser, cmb.anime, cmb.ep, cmb.aniep, cmb.xref, anifile })
                .Join(Repo.CrossRef_Subtitles_AniDB_File.GetAll(), s => s.anifile.FileID, audio => audio.FileID, (cmb, audio) => new { cmb.ser, cmb.anime, cmb.ep, cmb.aniep, cmb.xref, cmb.anifile, audio })
                .Join(Repo.Language.GetAll(), s => s.audio.LanguageID, lan => lan.LanguageID, (cmb, lan) => new { cmb.ser, cmb.anime, cmb.ep, cmb.aniep, cmb.xref, cmb.anifile, cmb.audio, lan })
                .Where(s => animeIds.Contains(s.anime.AnimeID))
                .Select(s => (s.anime.AnimeID, s.anime.MainTitle, s.lan.LanguageName))
                .Distinct();

            Dictionary<int, LanguageStat> dictStats = new Dictionary<int, LanguageStat>();

            foreach ((int animeID, string mainTitle, string lanName) in rows)
            {
                if (!dictStats.TryGetValue(animeID, out LanguageStat stat))
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