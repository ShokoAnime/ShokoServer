using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NHibernate;
using NutzCode.InMemoryIndex;
using Shoko.Abstractions.Extensions;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;

using EpisodeType = Shoko.Abstractions.Metadata.Enums.EpisodeType;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class AnimeEpisodeRepository : BaseCachedRepository<AnimeEpisode, int>
{
    private PocoIndex<int, AnimeEpisode, int>? _seriesIDs;

    private PocoIndex<int, AnimeEpisode, int>? _anidbEpisodeIDs;

    public AnimeEpisodeRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        BeginDeleteCallback = cr =>
        {
            RepoFactory.AnimeEpisode_User.Delete(
                RepoFactory.AnimeEpisode_User.GetByEpisodeID(cr.AnimeEpisodeID));
        };
    }

    protected override int SelectKey(AnimeEpisode entity)
        => entity.AnimeEpisodeID;

    public override void PopulateIndexes()
    {
        _seriesIDs = Cache.CreateIndex(a => a.AnimeSeriesID);
        _anidbEpisodeIDs = Cache.CreateIndex(a => a.AniDB_EpisodeID);
    }

    public List<AnimeEpisode> GetBySeriesID(int seriesID)
        => _seriesIDs!.GetMultiple(seriesID);

    public AnimeEpisode? GetByAniDBEpisodeID(int episodeID)
        => _anidbEpisodeIDs!.GetOne(episodeID);

    /// <summary>
    /// Get the AnimeEpisode
    /// </summary>
    /// <param name="name">The filename of the anime to search for.</param>
    /// <returns>the AnimeEpisode given the file information</returns>
    public AnimeEpisode? GetByFilename(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        var eps = RepoFactory.VideoLocalPlace.GetAll()
            .Where(v => name.Equals(v?.RelativePath?.Split(Path.DirectorySeparatorChar).LastOrDefault(), StringComparison.InvariantCultureIgnoreCase))
            .Select(a => RepoFactory.VideoLocal.GetByID(a.VideoID))
            .WhereNotNull()
            .SelectMany(a => GetByHash(a.Hash))
            .OrderBy(a => a.AniDB_Episode?.EpisodeType is EpisodeType.Episode)
            .ToArray();
        var ep = eps.FirstOrDefault(a => a.AniDB_Episode?.EpisodeType is EpisodeType.Episode);
        return ep ?? eps.FirstOrDefault();
    }


    /// <summary>
    /// Get all the AnimeEpisode records associate with an AniDB_File record
    /// AnimeEpisode.AniDB_EpisodeID -> AniDB_Episode.EpisodeID
    /// AniDB_Episode.EpisodeID -> CrossRef_File_Episode.EpisodeID
    /// CrossRef_File_Episode.Hash -> VideoLocal.Hash
    /// </summary>
    /// <param name="hash"></param>
    /// <returns></returns>
    public List<AnimeEpisode> GetByHash(string hash)
    {
        if (string.IsNullOrEmpty(hash))
            return [];

        return RepoFactory.CrossRef_File_Episode.GetByEd2k(hash)
            .Select(a => GetByAniDBEpisodeID(a.EpisodeID))
            .WhereNotNull()
            .ToList();
    }

    private const string MultipleReleasesIgnoreVariationsWithAnimeQuery =
        @"SELECT ani.EpisodeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE ani.AnimeID = :animeID AND vl.IsVariation = 0 AND vl.Hash != '' GROUP BY ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";
    private const string MultipleReleasesCountVariationsWithAnimeQuery =
        @"SELECT ani.EpisodeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE ani.AnimeID = :animeID AND vl.Hash != '' GROUP BY ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";
    private const string MultipleReleasesIgnoreVariationsQuery =
        @"SELECT ani.EpisodeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE vl.IsVariation = 0 AND vl.Hash != '' GROUP BY ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";
    private const string MultipleReleasesCountVariationsQuery =
        @"SELECT ani.EpisodeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE vl.Hash != '' GROUP BY ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";

    public IEnumerable<AnimeEpisode> GetWithMultipleReleases(bool ignoreVariations, int? animeID = null)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        IList<int> ids;
        if (animeID.HasValue && animeID.Value > 0)
        {
            var animeQuery = ignoreVariations ? MultipleReleasesIgnoreVariationsWithAnimeQuery : MultipleReleasesCountVariationsWithAnimeQuery;
            ids = session.CreateSQLQuery(animeQuery)
                .AddScalar("EpisodeID", NHibernateUtil.Int32)
                .SetParameter("animeID", animeID.Value)
                .List<int>();
        }
        else
        {
            var query = ignoreVariations ? MultipleReleasesIgnoreVariationsQuery : MultipleReleasesCountVariationsQuery;
            ids = session.CreateSQLQuery(query)
                .AddScalar("EpisodeID", NHibernateUtil.Int32)
                .List<int>();
        }

        return ids
            .Select(GetByAniDBEpisodeID)
            .Select(episode => (episode, anidbEpisode: episode?.AniDB_Episode))
            .Where(tuple => tuple.anidbEpisode is not null)
            .OrderBy(tuple => tuple.anidbEpisode!.AnimeID)
            .ThenBy(tuple => tuple.anidbEpisode!.EpisodeType)
            .ThenBy(tuple => tuple.anidbEpisode!.EpisodeNumber)
            .Select(tuple => tuple.episode!);
    }

    private const string DuplicateFilesWithAnimeQuery = @"
SELECT
    ani.EpisodeID
FROM
    (
        SELECT
            vl.FileSize,
            vl.Hash
        FROM
            VideoLocal AS vl
        WHERE
            VideoLocalID IN (
                SELECT
                    VideoLocalID
                FROM
                    VideoLocal_Place
                GROUP BY
                    VideoLocalID
                HAVING
                    COUNT(VideoLocal_Place_ID) > 1
            )
        AND
            vl.Hash != ''
    ) AS vlp_selected
INNER JOIN
    CrossRef_File_Episode ani
    ON vlp_selected.Hash = ani.Hash
       AND vlp_selected.FileSize = ani.FileSize
WHERE ani.AnimeID = :animeID
GROUP BY
    ani.EpisodeID
";

    private const string DuplicateFilesQuery = @"
SELECT
    ani.EpisodeID
FROM
    (
        SELECT
            vl.FileSize,
            vl.Hash
        FROM
            VideoLocal AS vl
        WHERE
            VideoLocalID IN (
                SELECT
                    VideoLocalID
                FROM
                    VideoLocal_Place
                GROUP BY
                    VideoLocalID
                HAVING
                    COUNT(VideoLocal_Place_ID) > 1
            )
        AND
            vl.Hash != ''
    ) AS vlp_selected
INNER JOIN
    CrossRef_File_Episode ani
    ON vlp_selected.Hash = ani.Hash
       AND vlp_selected.FileSize = ani.FileSize
GROUP BY
    ani.EpisodeID
";

    public IEnumerable<AnimeEpisode> GetWithDuplicateFiles(int? animeID = null)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        IList<int> ids;
        if (animeID.HasValue && animeID.Value > 0)
        {
            ids = session.CreateSQLQuery(DuplicateFilesWithAnimeQuery)
                .AddScalar("EpisodeID", NHibernateUtil.Int32)
                .SetParameter("animeID", animeID.Value)
                .List<int>();
        }
        else
        {
            ids = session.CreateSQLQuery(DuplicateFilesQuery)
                .AddScalar("EpisodeID", NHibernateUtil.Int32)
                .List<int>();
        }

        return ids
            .Select(GetByAniDBEpisodeID)
            .Select(episode => (episode, anidbEpisode: episode?.AniDB_Episode))
            .Where(tuple => tuple.anidbEpisode is not null)
            .OrderBy(tuple => tuple.anidbEpisode!.AnimeID)
            .ThenBy(tuple => tuple.anidbEpisode!.EpisodeType)
            .ThenBy(tuple => tuple.anidbEpisode!.EpisodeNumber)
            .Select(tuple => tuple.episode!);
    }

    // Group_CompletionStatus.Complete = 3, Group_CompletionStatus.Finished = 5
    // EpisodeType.Episode = 1
    // AirDate is stored as Unix timestamp (seconds). LastEpisodeNumber >= EpisodeNumber approximates HasGroupReleasedEpisode.
    // GS.GroupID (int) = SRI.GroupID (varchar) relies on implicit int↔string coercion present in SQLite, MySQL, and SQL Server.
    private const string MissingEpisodesQuery = @"
SELECT AE.AnimeEpisodeID
FROM AnimeEpisode AE
INNER JOIN AniDB_Episode ADBE ON AE.AniDB_EpisodeID = ADBE.EpisodeID
WHERE AE.IsHidden = 0
  AND ADBE.EpisodeType = 1
  AND ADBE.AirDate != 0
  AND ADBE.AirDate < :currentTime
  AND NOT EXISTS (SELECT 1 FROM CrossRef_File_Episode CFE WHERE CFE.EpisodeID = ADBE.EpisodeID)
  AND (
      NOT EXISTS (SELECT 1 FROM AniDB_GroupStatus GS WHERE GS.AnimeID = ADBE.AnimeID)
      OR EXISTS (
          SELECT 1 FROM AniDB_GroupStatus GS
          WHERE GS.AnimeID = ADBE.AnimeID
            AND (GS.CompletionState IN (3, 5) OR GS.LastEpisodeNumber >= ADBE.EpisodeNumber)
      )
  )
";

    private const string MissingEpisodesWithAnimeQuery = @"
SELECT AE.AnimeEpisodeID
FROM AnimeEpisode AE
INNER JOIN AniDB_Episode ADBE ON AE.AniDB_EpisodeID = ADBE.EpisodeID
WHERE AE.IsHidden = 0
  AND ADBE.EpisodeType = 1
  AND ADBE.AirDate != 0
  AND ADBE.AirDate < :currentTime
  AND ADBE.AnimeID = :animeID
  AND NOT EXISTS (SELECT 1 FROM CrossRef_File_Episode CFE WHERE CFE.EpisodeID = ADBE.EpisodeID)
  AND (
      NOT EXISTS (SELECT 1 FROM AniDB_GroupStatus GS WHERE GS.AnimeID = ADBE.AnimeID)
      OR EXISTS (
          SELECT 1 FROM AniDB_GroupStatus GS
          WHERE GS.AnimeID = ADBE.AnimeID
            AND (GS.CompletionState IN (3, 5) OR GS.LastEpisodeNumber >= ADBE.EpisodeNumber)
      )
  )
";

    private const string MissingCollectingEpisodesQuery = @"
SELECT AE.AnimeEpisodeID
FROM AnimeEpisode AE
INNER JOIN AniDB_Episode ADBE ON AE.AniDB_EpisodeID = ADBE.EpisodeID
WHERE AE.IsHidden = 0
  AND ADBE.EpisodeType = 1
  AND ADBE.AirDate != 0
  AND ADBE.AirDate < :currentTime
  AND NOT EXISTS (SELECT 1 FROM CrossRef_File_Episode CFE WHERE CFE.EpisodeID = ADBE.EpisodeID)
  AND EXISTS (
      SELECT 1 FROM AniDB_GroupStatus GS
      WHERE GS.AnimeID = ADBE.AnimeID
        AND (GS.CompletionState IN (3, 5) OR GS.LastEpisodeNumber >= ADBE.EpisodeNumber)
        AND EXISTS (
            SELECT 1 FROM StoredReleaseInfo SRI
            INNER JOIN CrossRef_File_Episode CFE2 ON SRI.ED2K = CFE2.Hash
            INNER JOIN AniDB_Episode ADBE2 ON CFE2.EpisodeID = ADBE2.EpisodeID
            WHERE ADBE2.AnimeID = ADBE.AnimeID
              AND ADBE2.EpisodeType = 1
              AND SRI.GroupSource = 'AniDB'
              AND SRI.GroupID IS NOT NULL
              AND GS.GroupID = SRI.GroupID
        )
  )
";

    private const string MissingCollectingEpisodesWithAnimeQuery = @"
SELECT AE.AnimeEpisodeID
FROM AnimeEpisode AE
INNER JOIN AniDB_Episode ADBE ON AE.AniDB_EpisodeID = ADBE.EpisodeID
WHERE AE.IsHidden = 0
  AND ADBE.EpisodeType = 1
  AND ADBE.AirDate != 0
  AND ADBE.AirDate < :currentTime
  AND ADBE.AnimeID = :animeID
  AND NOT EXISTS (SELECT 1 FROM CrossRef_File_Episode CFE WHERE CFE.EpisodeID = ADBE.EpisodeID)
  AND EXISTS (
      SELECT 1 FROM AniDB_GroupStatus GS
      WHERE GS.AnimeID = ADBE.AnimeID
        AND (GS.CompletionState IN (3, 5) OR GS.LastEpisodeNumber >= ADBE.EpisodeNumber)
        AND EXISTS (
            SELECT 1 FROM StoredReleaseInfo SRI
            INNER JOIN CrossRef_File_Episode CFE2 ON SRI.ED2K = CFE2.Hash
            INNER JOIN AniDB_Episode ADBE2 ON CFE2.EpisodeID = ADBE2.EpisodeID
            WHERE ADBE2.AnimeID = ADBE.AnimeID
              AND ADBE2.EpisodeType = 1
              AND SRI.GroupSource = 'AniDB'
              AND SRI.GroupID IS NOT NULL
              AND GS.GroupID = SRI.GroupID
        )
  )
";

    public IEnumerable<AnimeEpisode> GetMissing(bool collecting, int? animeID = null)
    {
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var session = _databaseFactory.SessionFactory.OpenSession();
        IList<int> ids;
        if (collecting)
        {
            if (animeID.HasValue)
                ids = session.CreateSQLQuery(MissingCollectingEpisodesWithAnimeQuery)
                    .AddScalar("AnimeEpisodeID", NHibernateUtil.Int32)
                    .SetParameter("currentTime", currentTime)
                    .SetParameter("animeID", animeID.Value)
                    .List<int>();
            else
                ids = session.CreateSQLQuery(MissingCollectingEpisodesQuery)
                    .AddScalar("AnimeEpisodeID", NHibernateUtil.Int32)
                    .SetParameter("currentTime", currentTime)
                    .List<int>();
        }
        else if (animeID.HasValue)
            ids = session.CreateSQLQuery(MissingEpisodesWithAnimeQuery)
                .AddScalar("AnimeEpisodeID", NHibernateUtil.Int32)
                .SetParameter("currentTime", currentTime)
                .SetParameter("animeID", animeID.Value)
                .List<int>();
        else
            ids = session.CreateSQLQuery(MissingEpisodesQuery)
                .AddScalar("AnimeEpisodeID", NHibernateUtil.Int32)
                .SetParameter("currentTime", currentTime)
                .List<int>();

        return ids
            .Select(GetByID)
            .WhereNotNull()
            .OrderBy(e => e.AniDB_Episode?.AnimeID)
            .ThenBy(e => e.AniDB_Episode?.EpisodeType)
            .ThenBy(e => e.AniDB_Episode?.EpisodeNumber);
    }

    public IReadOnlyList<AnimeEpisode> GetAllWatchedEpisodes(int userid, DateTime? after_date)
        => RepoFactory.AnimeEpisode_User.GetByUserID(userid)
            .Where(a => a.IsWatched && a.WatchedDate > after_date).OrderBy(a => a.WatchedDate)
            .Select(a => a.AnimeEpisode)
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<AnimeEpisode> GetEpisodesWithNoFiles(bool includeSpecials, bool includeOnlyAired = false)
        => GetAll()
            .Where(a =>
            {
                var anidbEpisode = a.AniDB_Episode;
                if (anidbEpisode is null || anidbEpisode.HasAired)
                    return false;

                if (anidbEpisode.EpisodeType is not EpisodeType.Episode and not EpisodeType.Special)
                    return false;

                if (!includeSpecials && anidbEpisode.EpisodeType is EpisodeType.Special)
                    return false;

                if (includeOnlyAired && !anidbEpisode.HasAired)
                    return false;

                return a.VideoLocals.Count == 0;
            })
            .OrderBy(a => a.AnimeSeries?.PreferredTitle)
            .ThenBy(a => a.AnimeSeriesID)
            .ToList();
}
