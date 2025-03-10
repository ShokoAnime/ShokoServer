using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NHibernate;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Services;

using AnimeType = Shoko.Models.Enums.AnimeType;
using EpisodeType = Shoko.Models.Enums.EpisodeType;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class AnimeEpisodeRepository : BaseCachedRepository<SVR_AnimeEpisode, int>
{
    private PocoIndex<int, SVR_AnimeEpisode, int>? _seriesIDs;

    private PocoIndex<int, SVR_AnimeEpisode, int>? _anidbEpisodeIDs;

    public AnimeEpisodeRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        BeginDeleteCallback = cr =>
        {
            RepoFactory.AnimeEpisode_User.Delete(
                RepoFactory.AnimeEpisode_User.GetByEpisodeID(cr.AnimeEpisodeID));
        };
    }

    protected override int SelectKey(SVR_AnimeEpisode entity)
        => entity.AnimeEpisodeID;

    public override void PopulateIndexes()
    {
        _seriesIDs = Cache.CreateIndex(a => a.AnimeSeriesID);
        _anidbEpisodeIDs = Cache.CreateIndex(a => a.AniDB_EpisodeID);
    }

    public List<SVR_AnimeEpisode> GetBySeriesID(int seriesID)
        => ReadLock(() => _seriesIDs!.GetMultiple(seriesID));

    public SVR_AnimeEpisode? GetByAniDBEpisodeID(int episodeID)
        => ReadLock(() => _anidbEpisodeIDs!.GetOne(episodeID));

    /// <summary>
    /// Get the AnimeEpisode
    /// </summary>
    /// <param name="name">The filename of the anime to search for.</param>
    /// <returns>the AnimeEpisode given the file information</returns>
    public SVR_AnimeEpisode? GetByFilename(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        var eps = RepoFactory.VideoLocalPlace.GetAll()
            .Where(v => name.Equals(v?.FilePath?.Split(Path.DirectorySeparatorChar).LastOrDefault(), StringComparison.InvariantCultureIgnoreCase))
            .Select(a => RepoFactory.VideoLocal.GetByID(a.VideoLocalID))
            .WhereNotNull()
            .SelectMany(a => GetByHash(a.Hash))
            .OrderBy(a => a.AniDB_Episode?.EpisodeTypeEnum is EpisodeType.Episode)
            .ToArray();
        var ep = eps.FirstOrDefault(a => a.AniDB_Episode?.EpisodeTypeEnum is EpisodeType.Episode);
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
    public List<SVR_AnimeEpisode> GetByHash(string hash)
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

    public IEnumerable<SVR_AnimeEpisode> GetWithMultipleReleases(bool ignoreVariations, int? animeID = null)
    {
        var ids = Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            if (animeID.HasValue && animeID.Value > 0)
            {
                var animeQuery = ignoreVariations ? MultipleReleasesIgnoreVariationsWithAnimeQuery : MultipleReleasesCountVariationsWithAnimeQuery;
                return session.CreateSQLQuery(animeQuery)
                    .AddScalar("EpisodeID", NHibernateUtil.Int32)
                    .SetParameter("animeID", animeID.Value)
                    .List<int>();
            }

            var query = ignoreVariations ? MultipleReleasesIgnoreVariationsQuery : MultipleReleasesCountVariationsQuery;
            return session.CreateSQLQuery(query)
                .AddScalar("EpisodeID", NHibernateUtil.Int32)
                .List<int>();
        });

        return ids
            .Select(GetByAniDBEpisodeID)
            .Select(episode => (episode, anidbEpisode: episode?.AniDB_Episode))
            .Where(tuple => tuple.anidbEpisode is not null)
            .OrderBy(tuple => tuple.anidbEpisode!.AnimeID)
            .ThenBy(tuple => tuple.anidbEpisode!.EpisodeTypeEnum)
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

    public IEnumerable<SVR_AnimeEpisode> GetWithDuplicateFiles(int? animeID = null)
    {
        var ids = Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            if (animeID.HasValue && animeID.Value > 0)
            {
                return session.CreateSQLQuery(DuplicateFilesWithAnimeQuery)
                    .AddScalar("EpisodeID", NHibernateUtil.Int32)
                    .SetParameter("animeID", animeID.Value)
                    .List<int>();
            }

            return session.CreateSQLQuery(DuplicateFilesQuery)
                .AddScalar("EpisodeID", NHibernateUtil.Int32)
                .List<int>();
        });

        return ids
            .Select(GetByAniDBEpisodeID)
            .Select(episode => (episode, anidbEpisode: episode?.AniDB_Episode))
            .Where(tuple => tuple.anidbEpisode is not null)
            .OrderBy(tuple => tuple.anidbEpisode!.AnimeID)
            .ThenBy(tuple => tuple.anidbEpisode!.EpisodeTypeEnum)
            .ThenBy(tuple => tuple.anidbEpisode!.EpisodeNumber)
            .Select(tuple => tuple.episode!);
    }

    public IEnumerable<SVR_AnimeEpisode> GetMissing(bool collecting, int? animeID = null)
    {
        // NOTE: For comments about this code, see the AnimeSeriesService.
        var allSeries = animeID.HasValue
            ? new List<SVR_AnimeSeries?>([RepoFactory.AnimeSeries.GetByAnimeID(animeID.Value)]).WhereNotNull()
            : RepoFactory.AnimeSeries.GetWithMissingEpisodes(collecting);
        foreach (var series in allSeries)
        {
            var animeType = (AnimeType)series.AniDB_Anime!.AnimeType;
            var episodeReleasedList = new AnimeSeriesService.EpisodeList(animeType);
            var episodeReleasedGroupList = new AnimeSeriesService.EpisodeList(animeType);
            var animeGroupStatuses = RepoFactory.AniDB_GroupStatus.GetByAnimeID(series.AniDB_ID);
            var allEpisodes = series.AllAnimeEpisodes
                .Select(episode => (episode, anidbEpisode: episode.AniDB_Episode!, videos: episode.VideoLocals))
                .Where(tuple => tuple.anidbEpisode is not null)
                .ToList();
            var localReleaseGroups = allEpisodes
                .Where(tuple => tuple.anidbEpisode.EpisodeTypeEnum == EpisodeType.Episode)
                .SelectMany(a => a.videos
                    .Select(b => b.ReleaseGroup)
                    .WhereNotNull()
                    .Where(b => b.Source is "AniDB" && int.TryParse(b.ID, out var groupID) && groupID > 0)
                    .Select(b => int.Parse(b.ID))
                )
                .ToHashSet();
            foreach (var (episode, anidbEpisode, videos) in allEpisodes)
            {
                if (anidbEpisode.EpisodeTypeEnum is not EpisodeType.Episode || videos.Count is not 0 || !anidbEpisode.HasAired)
                    continue;

                if (animeGroupStatuses.Count is 0)
                {
                    episodeReleasedList.Add(episode, videos.Count is not 0);
                    continue;
                }

                var filteredGroups = animeGroupStatuses
                    .Where(status =>
                        status.CompletionState is (int)Group_CompletionStatus.Complete or (int)Group_CompletionStatus.Finished ||
                        status.HasGroupReleasedEpisode(anidbEpisode.EpisodeNumber)
                    )
                    .ToList();
                if (filteredGroups.Count is 0)
                    continue;

                episodeReleasedList.Add(episode, videos.Count is not 0);
                if (filteredGroups.Any(a => localReleaseGroups.Contains(a.GroupID)))
                    episodeReleasedGroupList.Add(episode, videos.Count is not 0);
            }

            foreach (var episodeStats in collecting ? episodeReleasedGroupList : episodeReleasedList)
            {
                if (episodeStats.Available)
                    continue;

                foreach (var episodeStat in episodeStats)
                    if (!episodeStat.Episode.IsHidden)
                        yield return episodeStat.Episode;
            }
        }
    }

    public IReadOnlyList<SVR_AnimeEpisode> GetAllWatchedEpisodes(int userid, DateTime? after_date)
        => RepoFactory.AnimeEpisode_User.GetByUserID(userid)
            .Where(a => a.IsWatched && a.WatchedDate > after_date).OrderBy(a => a.WatchedDate)
            .Select(a => a.AnimeEpisode)
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<SVR_AnimeEpisode> GetEpisodesWithNoFiles(bool includeSpecials, bool includeOnlyAired = false)
        => GetAll()
            .Where(a =>
            {
                var anidbEpisode = a.AniDB_Episode;
                if (anidbEpisode is null || anidbEpisode.HasAired)
                    return false;

                if (anidbEpisode.EpisodeTypeEnum is not EpisodeType.Episode and not EpisodeType.Special)
                    return false;

                if (!includeSpecials && anidbEpisode.EpisodeTypeEnum is EpisodeType.Special)
                    return false;

                if (includeOnlyAired && !anidbEpisode.HasAired)
                    return false;

                return a.VideoLocals.Count == 0;
            })
            .OrderBy(a => a.AnimeSeries?.PreferredTitle)
            .ThenBy(a => a.AnimeSeriesID)
            .ToList();
}
