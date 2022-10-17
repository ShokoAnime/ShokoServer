using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate;
using Shoko.Commons.Collections;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories;

public class AdhocRepository : BaseRepository
{
    #region Video Quality

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

        if (animeGroupIds is { Count: 0 })
        {
            return EmptyLookup<int, string>.Instance;
        }

        var query = @"SELECT DISTINCT ag.AnimeGroupID, anifile.File_Source
FROM AnimeGroup ag
         INNER JOIN AnimeSeries ser
                    ON ser.AnimeGroupID = ag.AnimeGroupID
         INNER JOIN AniDB_Episode aniep
                    ON ser.AniDB_ID = aniep.AnimeID
         INNER JOIN CrossRef_File_Episode xref
                    ON aniep.EpisodeID = xref.EpisodeID
         INNER JOIN AniDB_File anifile
                    ON anifile.Hash = xref.Hash";

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
        return new HashSet<string>(RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID)
            .Select(a => a.GetAniDBFile()?.File_Source).Where(a => a != null));
        /*var vidQuals = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        var query =
            @$"SELECT distinct anifile.File_Source
FROM AnimeSeries ser
         INNER JOIN AniDB_Episode aniep on ser.AniDB_ID = aniep.AnimeID
         INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID
         INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash
WHERE ser.AniDB_ID = {animeID}
GROUP BY anifile.File_Source";

        lock (GlobalDBLock)
        {
            var command = session.Connection.CreateCommand();
            command.CommandText = query;

            using var rdr = command.ExecuteReader();
            while (rdr.Read())
            {
                var vidQual = rdr[0].ToString().Trim();
                vidQuals.Add(vidQual);
            }
        }

        return vidQuals;*/
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

        return animeIDs
            .Select(animeID => (animeID,
                new HashSet<string>(RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID)
                    .Select(a => a?.GetAniDBFile()?.File_Source).Where(a => a != null))))
            .ToDictionary(a => a.animeID, tuple => tuple.Item2);

        /*var allVidQualPerAnime = new Dictionary<int, HashSet<string>>();
        var query = @"SELECT aniep.AnimeID, anifile.File_Source
FROM AnimeSeries ser
         INNER JOIN AniDB_Episode aniep on ser.AniDB_ID = aniep.AnimeID
         INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID
         INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash
WHERE aniep.AnimeID IN ({0})
GROUP BY aniep.AnimeID, anifile.File_Source";

        lock (GlobalDBLock)
        {
            using var command = session.Connection.CreateCommand();
            command.CommandText = string.Format(query, string.Join(",", animeIDs));

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

        return allVidQualPerAnime;*/
    }

    public Dictionary<int, AnimeVideoQualityStat> GetEpisodeVideoQualityStatsByAnime(ISessionWrapper session,
        IReadOnlyCollection<int> animeIds = null)
    {
        var dictStats = new Dictionary<int, AnimeVideoQualityStat>();
        if (animeIds is not { Count: > 0 })
        {
            return dictStats;
        }

        return animeIds
            .SelectMany(animeID => RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID).Where(a =>
                    a.GetAniDBFile().Episodes
                        .Any(b => b.AnimeID == animeID && b.EpisodeType == (int)EpisodeType.Episode))
                .Select(a => (animeID, a.GetAniDBFile()?.File_Source)).Where(a => a.File_Source != null))
            .GroupBy(a => a.animeID).ToDictionary(a => a.Key,
                tuples => new AnimeVideoQualityStat
                {
                    AnimeID = tuples.Key,
                    VideoQualityEpisodeCount =
                        tuples.GroupBy(b => b.File_Source).ToDictionary(b => b.Key, b => b.Count())
                });
        
        /*lock (GlobalDBLock)
        {
            using var command = session.Connection.CreateCommand();
            command.CommandText = string.Format(@"SELECT aniep.AnimeID, anifile.File_Source, Count(aniep.EpisodeNumber) AS COUNT
FROM AnimeSeries ser
         INNER JOIN AniDB_Episode aniep on ser.AniDB_ID = aniep.AnimeID
         INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID
         INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash
WHERE aniep.EpisodeType = 1 AND aniep.AnimeID IN ({0})
GROUP BY aniep.AnimeID, anifile.File_Source
ORDER BY aniep.AnimeID, anifile.File_Source", string.Join(",", animeIds));

            using var rdr = command.ExecuteReader();
            while (rdr.Read())
            {
                var animeID = Convert.ToInt32(rdr[0]);
                var vidQual = rdr[1].ToString().Trim();
                var count = Convert.ToInt32(rdr[2]);

                if (!dictStats.TryGetValue(animeID, out var stat))
                {
                    stat = new AnimeVideoQualityStat
                    {
                        AnimeID = animeID,
                        VideoQualityEpisodeCount = new Dictionary<string, int>()
                    };
                    dictStats.Add(animeID, stat);
                }

                stat.VideoQualityEpisodeCount[vidQual] = count;
            }
        }

        return dictStats;*/
    }

    public AnimeVideoQualityStat GetEpisodeVideoQualityStatsForAnime(ISessionWrapper session, int aID)
    {
        return new AnimeVideoQualityStat
        {
            AnimeID = aID,
            VideoQualityEpisodeCount = RepoFactory.VideoLocal.GetByAniDBAnimeID(aID)
                .Where(a => a.GetAniDBFile().Episodes
                    .Any(b => b.AnimeID == aID && b.EpisodeType == (int)EpisodeType.Episode))
                .Select(a => a.GetAniDBFile()?.File_Source).Where(a => a != null).GroupBy(b => b)
                .ToDictionary(b => b.Key, b => b.Count())
        };
        /*var stat = new AnimeVideoQualityStat { VideoQualityEpisodeCount = new Dictionary<string, int>() };
        lock (GlobalDBLock)
        {
            var command = session.Connection.CreateCommand();
            command.CommandText = @$"SELECT aniep.AnimeID, anifile.File_Source, Count(aniep.EpisodeNumber) AS COUNT
FROM AnimeSeries ser
         INNER JOIN AniDB_Episode aniep on ser.AniDB_ID = aniep.AnimeID
         INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID
         INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash
WHERE aniep.EpisodeType = 1 AND aniep.AnimeID =  {aID}
GROUP BY aniep.AnimeID, anifile.File_Source";

            using var rdr = command.ExecuteReader();
            while (rdr.Read())
            {
                stat.AnimeID = Convert.ToInt32(rdr[0]);
                var vidQual = rdr[1].ToString().Trim();
                var count = Convert.ToInt32(rdr[2]);
                stat.VideoQualityEpisodeCount[vidQual] = count;
            }
        }

        return stat;*/
    }

    #endregion

    #region Audio and Subtitle Languages

    private Dictionary<int, HashSet<string>> GetAudioLanguageStatsByAnimeResults(ISessionWrapper session,
        string animeIdPredicate)
    {
        var query = @"SELECT DISTINCT aniep.AnimeID, audio.LanguageName 
FROM CrossRef_File_Episode xref 
    INNER JOIN AniDB_Episode aniep on aniep.EpisodeID = xref.EpisodeID
    INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash
    INNER JOIN CrossRef_Languages_AniDB_File audio on audio.FileID = anifile.FileID
WHERE aniep.AnimeID" + animeIdPredicate;

        IList<object[]> rows;
        lock (GlobalDBLock)
        {
            rows = session.CreateSQLQuery(query)
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .AddScalar("LanguageName", NHibernateUtil.String)
                .List<object[]>();
        }

        return rows.Select(cols => new { AnimeID = Convert.ToInt32(cols[0]), LanguageName = cols[1].ToString().Trim() })
            .GroupBy(a => a.AnimeID).ToDictionary(a => a.Key, a => a.Select(b => b.LanguageName).ToHashSet());
    }

    public Dictionary<int, HashSet<string>> GetAudioLanguageStatsByAnime(ISessionWrapper session, int aID)
    {
        return GetAudioLanguageStatsByAnimeResults(session, " = " + aID);
    }

    public Dictionary<int, HashSet<string>> GetAudioLanguageStatsByAnime(ISessionWrapper session,
        ICollection<int> aIDs)
    {
        if (aIDs.Count == 0)
        {
            return new Dictionary<int, HashSet<string>>();
        }

        var predicate = " IN (" + string.Join(",", aIDs) + ") ";

        return GetAudioLanguageStatsByAnimeResults(session, predicate);
    }

    public Dictionary<int, HashSet<string>> GetSubtitleLanguageStatsByAnime(ISessionWrapper session, int aID)
    {
        return GetSubtitleLanguageStatsByAnimeResults(session, " = " + aID);
    }

    public Dictionary<int, HashSet<string>> GetSubtitleLanguageStatsByAnime(ISessionWrapper session,
        ICollection<int> aIDs)
    {
        if (aIDs.Count == 0)
        {
            return new Dictionary<int, HashSet<string>>();
        }

        var predicate = " IN (" + string.Join(",", aIDs) + ") ";

        return GetSubtitleLanguageStatsByAnimeResults(session, predicate);
    }

    private Dictionary<int, HashSet<string>> GetSubtitleLanguageStatsByAnimeResults(ISessionWrapper session,
        string animeIdPredicate)
    {
        var dictStats = new Dictionary<int, HashSet<string>>();
        var query =
            @$"SELECT DISTINCT ser.AniDB_ID, audio.LanguageName 
FROM AnimeSeries ser 
    INNER JOIN AniDB_Episode aniep on aniep.AnimeID = ser.AniDB_ID
    INNER JOIN CrossRef_File_Episode xref on aniep.EpisodeID = xref.EpisodeID
    INNER JOIN AniDB_File anifile on anifile.Hash = xref.Hash
    INNER JOIN CrossRef_Subtitles_AniDB_File audio on audio.FileID = anifile.FileID
WHERE ser.AniDB_ID {animeIdPredicate}";

        IList<object[]> rows;
        lock (GlobalDBLock)
        {
            rows = session.CreateSQLQuery(query)
                .AddScalar("AniDB_ID", NHibernateUtil.Int32)
                .AddScalar("LanguageName", NHibernateUtil.String)
                .List<object[]>();
        }

        foreach (var cols in rows)
        {
            var animeID = Convert.ToInt32(cols[0]);
            var lanName = cols[1].ToString().Trim();

            if (!dictStats.TryGetValue(animeID, out var stat))
            {
                stat = new HashSet<string>();
                dictStats.Add(animeID, stat);
            }

            stat.Add(lanName);
        }

        return dictStats;
    }

    #endregion
}

public class AnimeVideoQualityStat
{
    public int AnimeID { get; set; }

    public Dictionary<string, int> VideoQualityEpisodeCount { get; set; }
    // video quality / number of episodes that match that quality
}
