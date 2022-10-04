using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Cached;

public class AnimeEpisodeRepository : BaseCachedRepository<SVR_AnimeEpisode, int>
{
    private PocoIndex<int, SVR_AnimeEpisode, int> Series;
    private PocoIndex<int, SVR_AnimeEpisode, int> EpisodeIDs;

    public AnimeEpisodeRepository()
    {
        BeginDeleteCallback = cr =>
        {
            RepoFactory.AnimeEpisode_User.Delete(
                RepoFactory.AnimeEpisode_User.GetByEpisodeID(cr.AnimeEpisodeID));
        };
    }

    protected override int SelectKey(SVR_AnimeEpisode entity)
    {
        return entity.AnimeEpisodeID;
    }

    public override void PopulateIndexes()
    {
        Series = Cache.CreateIndex(a => a.AnimeSeriesID);
        EpisodeIDs = Cache.CreateIndex(a => a.AniDB_EpisodeID);
    }

    public override void RegenerateDb()
    {
    }

    public List<SVR_AnimeEpisode> GetBySeriesID(int seriesid)
    {
        return ReadLock(() => Series.GetMultiple(seriesid));
    }


    public SVR_AnimeEpisode GetByAniDBEpisodeID(int epid)
    {
        return ReadLock(() => EpisodeIDs.GetOne(epid));
    }


    /// <summary>
    /// Get the AnimeEpisode 
    /// </summary>
    /// <param name="name">The filename of the anime to search for.</param>
    /// <returns>the AnimeEpisode given the file information</returns>
    public SVR_AnimeEpisode GetByFilename(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return RepoFactory.VideoLocalPlace.GetAll()
            .Where(v => name.Equals(v?.FilePath?.Split(Path.DirectorySeparatorChar).LastOrDefault(),
                StringComparison.InvariantCultureIgnoreCase))
            .Select(a => RepoFactory.VideoLocal.GetByID(a.VideoLocalID)).Where(a => a != null)
            .SelectMany(a => GetByHash(a.Hash))
            .FirstOrDefault();
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
        return RepoFactory.CrossRef_File_Episode.GetByHash(hash)
            .Select(a => GetByAniDBEpisodeID(a.EpisodeID))
            .Where(a => a != null)
            .ToList();
    }

    public List<SVR_AnimeEpisode> GetEpisodesWithMultipleFiles(bool ignoreVariations)
    {
        IEnumerable<int> ids;
        lock (GlobalDBLock)
        {
            const string ignoreVariationsQuery =
                @"SELECT ani.EpisodeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE vl.IsVariation = 0 AND vl.Hash != '' GROUP BY ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";
            const string countVariationsQuery =
                @"SELECT ani.EpisodeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE vl.Hash != '' GROUP BY ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";

            using var session = DatabaseFactory.SessionFactory.OpenSession();
            ids = ignoreVariations
                ? session.CreateSQLQuery(ignoreVariationsQuery).List<object>().Select(Convert.ToInt32)
                : session.CreateSQLQuery(countVariationsQuery).List<object>().Select(Convert.ToInt32);
        }

        return ids.Select(GetByAniDBEpisodeID).Where(a => a != null).ToList();
    }

    public List<SVR_AnimeEpisode> GetUnwatchedEpisodes(int seriesid, int userid)
    {
        var eps =
            RepoFactory.AnimeEpisode_User.GetByUserIDAndSeriesID(userid, seriesid)
                .Where(a => a.WatchedDate.HasValue)
                .Select(a => a.AnimeEpisodeID)
                .ToList();
        return GetBySeriesID(seriesid).Where(a => !eps.Contains(a.AnimeEpisodeID)).ToList();
    }

    public List<SVR_AnimeEpisode> GetAllWatchedEpisodes(int userid, DateTime? after_date)
    {
        var eps = RepoFactory.AnimeEpisode_User.GetByUserID(userid).Where(a => a.IsWatched())
            .Where(a => a.WatchedDate > after_date).OrderBy(a => a.WatchedDate).ToList();
        var list = new List<SVR_AnimeEpisode>();
        foreach (var ep in eps)
        {
            list.Add(GetByID(ep.AnimeEpisodeID));
        }

        return list;
    }

    public List<SVR_AnimeEpisode> GetEpisodesWithNoFiles(bool includeSpecials)
    {
        var all = GetAll().Where(a =>
            {
                var aniep = a.AniDB_Episode;
                if (aniep?.GetFutureDated() != false)
                {
                    return false;
                }

                if (aniep.EpisodeType != (int)EpisodeType.Episode &&
                    aniep.EpisodeType != (int)EpisodeType.Special)
                {
                    return false;
                }

                if (!includeSpecials &&
                    aniep.EpisodeType == (int)EpisodeType.Special)
                {
                    return false;
                }

                return a.GetVideoLocals().Count == 0;
            })
            .ToList();
        all.Sort((a1, a2) =>
        {
            var name1 = a1.GetAnimeSeries()?.GetSeriesName();
            var name2 = a2.GetAnimeSeries()?.GetSeriesName();

            if (!string.IsNullOrEmpty(name1) && !string.IsNullOrEmpty(name2))
            {
                return string.Compare(name1, name2, StringComparison.Ordinal);
            }

            if (string.IsNullOrEmpty(name1))
            {
                return 1;
            }

            if (string.IsNullOrEmpty(name2))
            {
                return -1;
            }

            return a1.AnimeSeriesID.CompareTo(a2.AnimeSeriesID);
        });

        return all;
    }
}
