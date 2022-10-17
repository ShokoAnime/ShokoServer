using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using NHibernate;
using Shoko.Commons.Collections;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories;

public class AdhocRepository : BaseRepository
{
    #region Video Quality

    /// <summary>
    /// Gets a list fo all the possible video quality settings for the user e.g. dvd, blu-ray
    /// </summary>
    /// <returns></returns>
    public List<string> GetAllVideoQuality()
    {
        var allVidQuality = new List<string>();

        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            IDbCommand command = session.Connection.CreateCommand();
            command.CommandText = "SELECT Distinct(File_Source) FROM AniDB_File";

            using var rdr = command.ExecuteReader();
            while (rdr.Read())
            {
                var vidQual = rdr[0].ToString().Trim();
                allVidQuality.Add(vidQual);
            }
        }

        return allVidQuality;
    }

    /// <summary>
    /// Gets All video quality by group.
    /// </summary>
    /// <param name="session">The NHibernate session.</param>
    /// <param name="animeGroupIds">The optional list of group IDs to limit the results to.
    /// If <c>null</c> is specified, then results for ALL groups will be returned.</param>
    /// <returns>A <see cref="ILookup{TKey,TElement}"/> containing all video quality grouped by anime group ID.</returns>
    public ILookup<int, string> GetAllVideoQualityByGroup(ISessionWrapper session,
        IReadOnlyCollection<int> animeGroupIds = null)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (animeGroupIds != null && animeGroupIds.Count == 0)
        {
            return EmptyLookup<int, string>.Instance;
        }

        var query = @"
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
                    WHERE ag.AnimeGroupID IN (" + string.Join(",", animeGroupIds) + ")";
        }

        ILookup<int, string> results;
        lock (GlobalDBLock)
        {
            results = session.CreateSQLQuery(query)
                .AddScalar("AnimeGroupID", NHibernateUtil.Int32)
                .AddScalar("File_Source", NHibernateUtil.String)
                .List<object[]>()
                .ToLookup(r => (int)r[0], r => (string)r[1]);
        }

        return results;
    }

    public HashSet<string> GetAllVideoQualityForAnime(ISessionWrapper session, int animeID)
    {
        var vidQuals = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        var query =
            @$"SELECT anifile.File_Source
    FROM AnimeSeries ser
    INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID
    INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID
    INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID
    INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID
    INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash
    INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID
    WHERE anime.AnimeID = {animeID}
    GROUP BY anifile.File_Source";

        lock (GlobalDBLock)
        {
            var command = session.Connection.CreateCommand();
            command.CommandText = query;

            using var rdr = command.ExecuteReader();
            while (rdr.Read())
            {
                var vidQual = rdr[0].ToString().Trim();
                if (!vidQuals.Contains(vidQual))
                {
                    vidQuals.Add(vidQual);
                }
            }
        }

        return vidQuals;
    }

    public Dictionary<int, HashSet<string>> GetAllVideoQualityByAnime(ISessionWrapper session,
        ICollection<int> animeIDs)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (animeIDs == null)
        {
            throw new ArgumentNullException(nameof(animeIDs));
        }

        if (animeIDs.Count == 0)
        {
            return new Dictionary<int, HashSet<string>>();
        }

        var allVidQualPerAnime = new Dictionary<int, HashSet<string>>();
        var query = $@"SELECT anime.AnimeID, anifile.File_Source
                    FROM AnimeSeries ser
                    INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID
                    INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID
                    INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID
                    INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID
                    INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash
                    INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID
                    WHERE anime.AnimeID IN ({string.Join(",", animeIDs)})
                    GROUP BY anime.AnimeID, anifile.File_Source ";

        lock (GlobalDBLock)
        {
            using var command = session.Connection.CreateCommand();
            command.CommandText = query;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var animeId = Convert.ToInt32(reader[0]);
                var vidQual = reader[1].ToString().Trim();

                if (!allVidQualPerAnime.TryGetValue(animeId, out var vidQualSet))
                {
                    vidQualSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                    allVidQualPerAnime.Add(animeId, vidQualSet);
                }

                vidQualSet.Add(vidQual);
            }
        }

        return allVidQualPerAnime;
    }

    public Dictionary<int, AnimeVideoQualityStat> GetEpisodeVideoQualityStatsByAnime(ISessionWrapper session,
        IReadOnlyCollection<int> animeIds = null)
    {
        var dictStats = new Dictionary<int, AnimeVideoQualityStat>();
        if (animeIds is not { Count: > 0 })
        {
            return dictStats;
        }

        lock (GlobalDBLock)
        {
            using var command = session.Connection.CreateCommand();
            command.CommandText = @$"SELECT anime.AnimeID, anime.MainTitle, anifile.File_Source, aniep.EpisodeNumber
    FROM AnimeSeries ser
        INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID
        INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID
        INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID
        INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID
        INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash
        INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID
    WHERE aniep.EpisodeType = 1 AND anime.AnimeID IN ({string.Join(",", animeIds)})
    GROUP BY anime.AnimeID, anime.MainTitle, anifile.File_Source, aniep.EpisodeNumber
    ORDER BY anime.AnimeID, anime.MainTitle, anifile.File_Source, aniep.EpisodeNumber";

            using var rdr = command.ExecuteReader();
            while (rdr.Read())
            {
                var animeID = Convert.ToInt32(rdr[0]);
                var mainTitle = rdr[1].ToString().Trim();
                var vidQual = rdr[2].ToString().Trim();

                if (!dictStats.TryGetValue(animeID, out var stat))
                {
                    stat = new AnimeVideoQualityStat
                    {
                        AnimeID = animeID,
                        MainTitle = mainTitle,
                        VideoQualityEpisodeCount = new Dictionary<string, int>()
                    };
                    dictStats.Add(animeID, stat);
                }


                stat.VideoQualityEpisodeCount.TryGetValue(vidQual, out var epCount);
                stat.VideoQualityEpisodeCount[vidQual] = epCount + 1;
            }
        }

        return dictStats;
    }

    public AnimeVideoQualityStat GetEpisodeVideoQualityStatsForAnime(ISessionWrapper session, int aID)
    {
        var stat = new AnimeVideoQualityStat { VideoQualityEpisodeCount = new Dictionary<string, int>() };
        lock (GlobalDBLock)
        {
            var command = session.Connection.CreateCommand();
            command.CommandText = @$"SELECT anime.AnimeID, anime.MainTitle, anifile.File_Source, aniep.EpisodeNumber
    FROM AnimeSeries ser
        INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID
        INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID
        INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID
        INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID
        INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash
        INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID
    WHERE aniep.EpisodeType = 1 AND anime.AnimeID =  {aID}
    GROUP BY anime.AnimeID, anime.MainTitle, anifile.File_Source, aniep.EpisodeNumber ";

            using var rdr = command.ExecuteReader();
            while (rdr.Read())
            {
                stat.AnimeID = int.Parse(rdr[0].ToString());
                stat.MainTitle = rdr[1].ToString().Trim();

                var vidQual = rdr[2].ToString().Trim();
                var epNumber = int.Parse(rdr[3].ToString());

                if (!stat.VideoQualityEpisodeCount.ContainsKey(vidQual))
                {
                    stat.VideoQualityEpisodeCount[vidQual] = 1;
                }
                else
                {
                    stat.VideoQualityEpisodeCount[vidQual]++;
                }
            }
        }

        return stat;
    }

    #endregion

    #region Audio and Subtitle Languages

    /// <summary>
    /// Gets a list of all the possible audio languages
    /// </summary>
    /// <returns></returns>
    public List<string> GetAllUniqueAudioLanguages()
    {
        var allLanguages = new List<string>();

        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            IDbCommand command = session.Connection.CreateCommand();
#pragma warning disable 2100
            command.CommandText = "SELECT Distinct(lan.LanguageName) ";
            command.CommandText += "FROM CrossRef_Languages_AniDB_File audio ";
            command.CommandText += "INNER JOIN Language lan on audio.LanguageID = lan.LanguageID ";
            command.CommandText += "ORDER BY lan.LanguageName ";
#pragma warning restore 2100

            using var rdr = command.ExecuteReader();
            while (rdr.Read())
            {
                var lan = rdr[0].ToString().Trim();
                allLanguages.Add(lan);
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
        var allLanguages = new List<string>();

        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            IDbCommand command = session.Connection.CreateCommand();
            command.CommandText = "SELECT Distinct(LanguageName) ";
            command.CommandText += "FROM CrossRef_Subtitles_AniDB_File";
            command.CommandText += "ORDER BY LanguageName ";

            using var rdr = command.ExecuteReader();
            while (rdr.Read())
            {
                var lan = rdr[0].ToString().Trim();
                allLanguages.Add(lan);
            }
        }

        return allLanguages;
    }

    private Dictionary<int, LanguageStat> GetAudioLanguageStatsByAnimeResults(ISessionWrapper session,
        string animeIdPredicate)
    {
        var dictStats = new Dictionary<int, LanguageStat>();
        var query = "SELECT DISTINCT anime.AnimeID, anime.MainTitle, audio.LanguageName "
                    + "FROM AnimeSeries ser  "
                    + "INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID "
                    + "INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID "
                    + "INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID "
                    + "INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID "
                    + "INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash "
                    + "INNER JOIN CrossRef_Languages_AniDB_File audio on audio.FileID = anifile.FileID "
                    + "WHERE anime.AnimeID " + animeIdPredicate;

        IList<object[]> rows;
        lock (GlobalDBLock)
        {
            rows = session.CreateSQLQuery(query)
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .AddScalar("MainTitle", NHibernateUtil.String)
                .AddScalar("LanguageName", NHibernateUtil.String)
                .List<object[]>();
        }

        foreach (var cols in rows)
        {
            var animeID = Convert.ToInt32(cols[0]);
            var mainTitle = cols[1].ToString().Trim();
            var lanName = cols[2].ToString().Trim();

            if (!dictStats.TryGetValue(animeID, out var stat))
            {
                stat = new LanguageStat
                {
                    AnimeID = animeID, MainTitle = mainTitle, LanguageNames = new List<string>()
                };
                dictStats.Add(animeID, stat);
            }

            stat.LanguageNames.Add(lanName);
        }

        return dictStats;
    }

    public Dictionary<int, LanguageStat> GetAudioLanguageStatsByAnime(ISessionWrapper session, int aID)
    {
        return GetAudioLanguageStatsByAnimeResults(session, " = " + aID);
    }

    public Dictionary<int, LanguageStat> GetAudioLanguageStatsByAnime(ISessionWrapper session,
        ICollection<int> aIDs)
    {
        if (aIDs.Count == 0)
        {
            return new Dictionary<int, LanguageStat>();
        }

        var predicate = " IN (" + string.Join(",", aIDs) + ") ";

        return GetAudioLanguageStatsByAnimeResults(session, predicate);
    }

    public Dictionary<int, LanguageStat> GetSubtitleLanguageStatsByAnime(ISessionWrapper session, int aID)
    {
        return GetSubtitleLanguageStatsByAnimeResults(session, " = " + aID);
    }

    public Dictionary<int, LanguageStat> GetSubtitleLanguageStatsByAnime(ISessionWrapper session,
        ICollection<int> aIDs)
    {
        if (aIDs.Count == 0)
        {
            return new Dictionary<int, LanguageStat>();
        }

        var predicate = " IN (" + string.Join(",", aIDs) + ") ";

        return GetSubtitleLanguageStatsByAnimeResults(session, predicate);
    }

    private Dictionary<int, LanguageStat> GetSubtitleLanguageStatsByAnimeResults(ISessionWrapper session,
        string animeIdPredicate)
    {
        var dictStats = new Dictionary<int, LanguageStat>();
        var query =
            @$"SELECT DISTINCT anime.AnimeID, anime.MainTitle, subt.LanguageName
    FROM AnimeSeries ser
        INNER JOIN AniDB_Anime anime on anime.AnimeID = ser.AniDB_ID
        INNER JOIN AnimeEpisode ep on ep.AnimeSeriesID = ser.AnimeSeriesID
        INNER JOIN AniDB_Episode aniep on ep.AniDB_EpisodeID = aniep.EpisodeID
        INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID
        INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash
        INNER JOIN CrossRef_Subtitles_AniDB_File subt on subt.FileID = anifile.FileID
    WHERE anime.AnimeID {animeIdPredicate}";

        IList<object[]> rows;
        lock (GlobalDBLock)
        {
            rows = session.CreateSQLQuery(query)
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .AddScalar("MainTitle", NHibernateUtil.String)
                .AddScalar("LanguageName", NHibernateUtil.String)
                .List<object[]>();
        }

        foreach (var cols in rows)
        {
            var animeID = Convert.ToInt32(cols[0]);
            var mainTitle = cols[1].ToString().Trim();
            var lanName = cols[2].ToString().Trim();

            if (!dictStats.TryGetValue(animeID, out var stat))
            {
                stat = new LanguageStat
                {
                    AnimeID = animeID, MainTitle = mainTitle, LanguageNames = new List<string>()
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
