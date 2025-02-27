using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FluentNHibernate.Utils;
using Microsoft.Extensions.DependencyInjection;
using NutzCode.InMemoryIndex;
using Quartz;
using Shoko.Server.Databases;
using Shoko.Server.Exceptions;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Utilities;

#nullable enable
#pragma warning disable CS0618
#pragma warning disable CA2012
namespace Shoko.Server.Repositories.Cached;

public class VideoLocalRepository : BaseCachedRepository<VideoLocal, int>
{
    private PocoIndex<int, VideoLocal, string>? _ed2k;

    private PocoIndex<int, VideoLocal, bool>? _ignored;

    public VideoLocalRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        DeleteWithOpenTransactionCallback = (ses, obj) =>
        {
            RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(ses, obj.Places.ToList());
            RepoFactory.VideoLocalUser.DeleteWithOpenTransaction(ses, RepoFactory.VideoLocalUser.GetByVideoLocalID(obj.VideoLocalID));
            RepoFactory.VideoLocalHashDigest.DeleteWithOpenTransaction(ses, RepoFactory.VideoLocalHashDigest.GetByVideoLocalID(obj.VideoLocalID));
        };
    }

    protected override int SelectKey(VideoLocal entity)
        => entity.VideoLocalID;

    public override void PopulateIndexes()
    {
        //Fix null hashes
        foreach (var l in Cache.Values)
        {
            if (l.Hash != null && l.FileName != null) continue;

            l.MediaVersion = 0;
            l.Hash ??= string.Empty;
            l.FileName ??= string.Empty;
        }

        _ed2k = Cache.CreateIndex(a => a.Hash);
        _ignored = Cache.CreateIndex(a => a.IsIgnored);
    }

    public override void RegenerateDb()
    {
        ServerState.Instance.ServerStartingStatus = $"Database - Validating - {nameof(VideoLocal)} Checking Media Info...";
        var count = 0;
        int max;
        IReadOnlyList<VideoLocal> list;

        try
        {
            list = Cache.Values.Where(a => a.MediaVersion < VideoLocal.MEDIA_VERSION || a.MediaInfo == null).ToList();
            max = list.Count;

            var scheduler = Utils.ServiceContainer.GetRequiredService<ISchedulerFactory>().GetScheduler().Result;
            list.ForEach(
                a =>
                {
                    scheduler.StartJob<MediaInfoJob>(c => c.VideoLocalID = a.VideoLocalID).GetAwaiter().GetResult();
                    count++;
                    ServerState.Instance.ServerStartingStatus = $"Database - Validating - {nameof(VideoLocal)} Queuing Media Info Commands - {count}/{max}...";
                }
            );
        }
        catch
        {
            // ignore
        }

        var locals = Cache.Values
            .Where(a => !string.IsNullOrWhiteSpace(a.Hash))
            .GroupBy(a => a.Hash)
            .ToDictionary(g => g.Key, g => g.ToList());
        ServerState.Instance.ServerStartingStatus = $"Database - Validating - {nameof(VideoLocal)} Cleaning Empty Records...";
        using var session = _databaseFactory.SessionFactory.OpenSession();
        using (var transaction = session.BeginTransaction())
        {
            list = Cache.Values.Where(a => a.IsEmpty()).ToList();
            count = 0;
            max = list.Count;
            foreach (var remove in list)
            {
                RepoFactory.VideoLocal.DeleteWithOpenTransaction(session, remove);
                count++;
                ServerState.Instance.ServerStartingStatus =
                    $"Database - Validating - {nameof(VideoLocal)} Cleaning Empty Records - {count}/{max}...";
            }

            transaction.Commit();
        }

        var toRemove = new List<VideoLocal>();
        var comparer = new VideoLocalComparer();

        ServerState.Instance.ServerStartingStatus = $"Database - Validating - {nameof(VideoLocal)} Checking for Duplicate Records...";

        foreach (var hash in locals.Keys)
        {
            var values = locals[hash];
            values.Sort(comparer);
            var to = values.First();
            var fromList = values.Except(to).ToList();
            foreach (var from in fromList)
            {
                var places = from.Places;
                if (places == null || places.Count == 0)
                {
                    continue;
                }

                using var transaction = session.BeginTransaction();
                foreach (var place in places)
                {
                    place.VideoLocalID = to.VideoLocalID;
                    RepoFactory.VideoLocalPlace.SaveWithOpenTransaction(session, place);
                }

                transaction.Commit();
            }

            toRemove.AddRange(fromList);
        }

        count = 0;
        max = toRemove.Count;
        foreach (var batch in toRemove.Batch(50))
        {
            using var transaction = session.BeginTransaction();
            foreach (var remove in batch)
            {
                count++;
                ServerState.Instance.ServerStartingStatus = $"Database - Validating - {nameof(VideoLocal)} Cleaning Duplicate Records - {count}/{max}...";
                DeleteWithOpenTransaction(session, remove);
            }

            transaction.Commit();
        }
    }

    public IReadOnlyList<VideoLocal> GetByImportFolder(int importFolderID)
        => RepoFactory.VideoLocalPlace.GetByImportFolder(importFolderID)
            .Select(a => GetByID(a.VideoLocalID))
            .WhereNotNull()
            .Distinct()
            .ToList();

    public override void Delete(VideoLocal obj)
    {
        var list = obj.AnimeEpisodes;
        base.Delete(obj);
        list.WhereNotNull().ForEach(a => RepoFactory.AnimeEpisode.Save(a));
    }

    public override void Save(VideoLocal obj)
    {
        Save(obj, true);
    }

    public void Save(VideoLocal obj, bool updateEpisodes)
    {
        if (obj.VideoLocalID == 0)
        {
            obj.MediaInfo = null;
            base.Save(obj);
        }

        UpdateMediaContracts(obj);
        base.Save(obj);

        if (updateEpisodes)
        {
            RepoFactory.AnimeEpisode.Save(obj.AnimeEpisodes);
        }
    }

    private static void UpdateMediaContracts(VideoLocal obj)
    {
        if (obj.MediaInfo != null && obj.MediaVersion >= VideoLocal.MEDIA_VERSION)
        {
            return;
        }

        var place = obj.FirstResolvedPlace;
        if (place != null) Utils.ServiceContainer.GetRequiredService<VideoLocal_PlaceService>().RefreshMediaInfo(place);
    }

    public VideoLocal? GetByEd2k(string hash)
    {
        if (string.IsNullOrEmpty(hash))
            throw new InvalidStateException("Trying to lookup a VideoLocal by an empty Hash");

        return ReadLock(() => _ed2k!.GetOne(hash));
    }

    public VideoLocal? GetByEd2kAndSize(string hash, long fileSize)
    {
        if (string.IsNullOrEmpty(hash))
            throw new InvalidStateException("Trying to lookup a VideoLocal by an empty Hash");

        if (fileSize <= 0)
            throw new InvalidStateException("Trying to lookup a VideoLocal by a filesize of 0");

        return ReadLock(() => _ed2k!.GetMultiple(hash).FirstOrDefault(a => a.FileSize == fileSize));
    }

    public VideoLocal? GetByMd5(string hash)
    {
        if (string.IsNullOrEmpty(hash))
            throw new InvalidStateException("Trying to lookup a VideoLocal by an empty MD5");

        return RepoFactory.VideoLocalHashDigest.GetByHashTypeAndValue("MD5", hash)
            .Select(a => GetByID(a.VideoLocalID))
            .WhereNotNull()
            .FirstOrDefault();
    }

    public VideoLocal? GetByMd5AndSize(string hash, long fileSize)
    {
        if (string.IsNullOrEmpty(hash))
            throw new InvalidStateException("Trying to lookup a VideoLocal by an empty MD5");

        if (fileSize <= 0)
            throw new InvalidStateException("Trying to lookup a VideoLocal by a filesize of 0");

        return RepoFactory.VideoLocalHashDigest.GetByHashTypeAndValue("MD5", hash)
            .Select(a => GetByID(a.VideoLocalID))
            .WhereNotNull()
            .FirstOrDefault(a => a.FileSize == fileSize);
    }

    public VideoLocal? GetBySha1(string hash)
    {
        if (string.IsNullOrEmpty(hash))
            throw new InvalidStateException("Trying to lookup a VideoLocal by an empty SHA1");

        return RepoFactory.VideoLocalHashDigest.GetByHashTypeAndValue("SHA1", hash)
            .Select(a => GetByID(a.VideoLocalID))
            .WhereNotNull()
            .FirstOrDefault();
    }

    public VideoLocal? GetBySha1AndSize(string hash, long fileSize)
    {
        if (string.IsNullOrEmpty(hash))
            throw new InvalidStateException("Trying to lookup a VideoLocal by an empty SHA1");

        if (fileSize <= 0)
            throw new InvalidStateException("Trying to lookup a VideoLocal by a filesize of 0");

        return RepoFactory.VideoLocalHashDigest.GetByHashTypeAndValue("SHA1", hash)
            .Select(a => GetByID(a.VideoLocalID))
            .WhereNotNull()
            .FirstOrDefault(a => a.FileSize == fileSize);
    }

    public VideoLocal? GetByCrc32(string hash)
    {
        if (string.IsNullOrEmpty(hash))
            throw new InvalidStateException("Trying to lookup a VideoLocal by an empty CRC32");

        return RepoFactory.VideoLocalHashDigest.GetByHashTypeAndValue("CRC32", hash)
            .Select(a => GetByID(a.VideoLocalID))
            .WhereNotNull()
            .FirstOrDefault();
    }

    public VideoLocal? GetByCrc32AndSize(string hash, long fileSize)
    {
        if (string.IsNullOrEmpty(hash))
            throw new InvalidStateException("Trying to lookup a VideoLocal by an empty CRC32");

        if (fileSize <= 0)
            throw new InvalidStateException("Trying to lookup a VideoLocal by a filesize of 0");

        return RepoFactory.VideoLocalHashDigest.GetByHashTypeAndValue("CRC32", hash)
            .Select(a => GetByID(a.VideoLocalID))
            .WhereNotNull()
            .FirstOrDefault(a => a.FileSize == fileSize);
    }

    public IReadOnlyList<VideoLocal> GetByName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            throw new InvalidStateException("Trying to lookup a VideoLocal by an empty Filename");

        return ReadLock(() => Cache.Values
            .Where(p => p.Places.Any(a => a.FilePath.FuzzyMatch(fileName)))
            .ToList()
        );
    }

    public IReadOnlyList<VideoLocal> GetMostRecentlyAdded(int maxResults, int userID)
    {
        var user = RepoFactory.JMMUser.GetByID(userID);
        if (user == null)
            return ReadLock(() => maxResults < 0
                ? Cache.Values.OrderByDescending(a => a.DateTimeCreated).ToList()
                : Cache.Values.OrderByDescending(a => a.DateTimeCreated).Take(maxResults).ToList());

        if (maxResults < 0)
            return ReadLock(() => Cache.Values
                .Where(a => a.AnimeEpisodes
                    .Select(b => b.AnimeSeries)
                    .WhereNotNull()
                    .DistinctBy(b => b.AniDB_ID)
                    .All(user.AllowedSeries)
                ).OrderByDescending(a => a.DateTimeCreated)
                .ToList()
            );

        return ReadLock(() => Cache.Values
            .Where(a => a.AnimeEpisodes
                .Select(b => b.AnimeSeries)
                .WhereNotNull()
                .DistinctBy(b => b.AniDB_ID)
                .All(user.AllowedSeries)
            ).OrderByDescending(a => a.DateTimeCreated)
            .Take(maxResults).ToList()
        );
    }

    public IReadOnlyList<VideoLocal> GetMostRecentlyAdded(int take, int skip, int userID)
    {
        if (skip < 0)
            skip = 0;

        if (take == 0)
            return [];

        var user = userID == -1 ? null : RepoFactory.JMMUser.GetByID(userID);
        if (user == null)
        {
            return ReadLock(() => take < 0
                ? Cache.Values.OrderByDescending(a => a.DateTimeCreated).Skip(skip).ToList()
                : Cache.Values.OrderByDescending(a => a.DateTimeCreated).Skip(skip).Take(take).ToList());
        }

        return ReadLock(() => take < 0
            ? Cache.Values
                .Where(a => a.AnimeEpisodes
                    .Select(b => b.AnimeSeries)
                    .WhereNotNull()
                    .DistinctBy(b => b.AniDB_ID)
                    .All(user.AllowedSeries)
                )
                .OrderByDescending(a => a.DateTimeCreated)
                .Skip(skip)
                .ToList()
            : Cache.Values
                .Where(a => a.AnimeEpisodes
                    .Select(b => b.AnimeSeries)
                    .WhereNotNull()
                    .DistinctBy(b => b.AniDB_ID)
                    .All(user.AllowedSeries)
                )
                .OrderByDescending(a => a.DateTimeCreated)
                .Skip(skip)
                .Take(take)
                .ToList()
        );
    }

    public IReadOnlyList<VideoLocal> GetRandomFiles(int maxResults)
    {
        var values = ReadLock(Cache.Values.ToList).Where(a => a.EpisodeCrossReferences.Any()).ToList();

        using var en = new UniqueRandoms(0, values.Count - 1).GetEnumerator();
        var list = new List<VideoLocal>();
        if (maxResults > values.Count)
            maxResults = values.Count;

        while (en.MoveNext())
        {
            list.Add(values.ElementAt(en.Current));
            if (list.Count >= maxResults)
                break;
        }

        return list;
    }

    public class UniqueRandoms : IEnumerable<int>
    {
        private readonly Random _rand = new();
        private readonly List<int> _candidates;

        public UniqueRandoms(int minInclusive, int maxInclusive)
        {
            _candidates = Enumerable.Range(minInclusive, maxInclusive - minInclusive + 1).ToList();
        }

        public IEnumerator<int> GetEnumerator()
        {
            while (_candidates.Count > 0)
            {
                var index = _rand.Next(_candidates.Count);
                yield return _candidates[index];
                _candidates.RemoveAt(index);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }


    /// <summary>
    /// returns all the VideoLocal records associate with an AnimeEpisode Record
    /// </summary>
    /// <param name="episodeID">AniDB Episode ID</param>
    /// <returns></returns>
    /// 
    public IReadOnlyList<VideoLocal> GetByAniDBEpisodeID(int episodeID)
        => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(episodeID)
            .Select(a => GetByEd2k(a.Hash))
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<VideoLocal> GetMostRecentlyAddedForAnime(int maxResults, int animeID)
        => RepoFactory.CrossRef_File_Episode.GetByAnimeID(animeID)
                .Select(a => GetByEd2k(a.Hash))
                .WhereNotNull()
                .OrderByDescending(a => a.DateTimeCreated)
                .Take(maxResults)
                .ToList();

    /// <summary>
    /// returns all the VideoLocal records associate with an AniDB_Anime Record
    /// </summary>
    /// <param name="animeID">AniDB Anime ID</param>
    /// <returns></returns>
    public IReadOnlyList<VideoLocal> GetByAniDBAnimeID(int animeID)
        => RepoFactory.CrossRef_File_Episode.GetByAnimeID(animeID)
            .Select(xref => GetByEd2k(xref.Hash))
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<VideoLocal> GetVideosWithoutHash()
        => ReadLock(() => _ed2k!.GetMultiple(""));

    public IReadOnlyList<VideoLocal> GetVideosWithoutEpisode(bool includeBrokenXRefs = false)
        => ReadLock(() => Cache.Values
            .Where(a =>
            {
                if (a.IsIgnored)
                    return false;

                var xrefs = RepoFactory.CrossRef_File_Episode.GetByEd2k(a.Hash);
                if (!xrefs.Any())
                    return true;

                if (includeBrokenXRefs)
                    return !xrefs.Any(IsImported);

                return false;
            })
            .OrderByNatural(local =>
            {
                var place = local?.FirstValidPlace;
                if (place == null) return null;
                return place.FullServerPath ?? place.FilePath;
            })
            .ThenBy(local => local?.VideoLocalID ?? 0)
            .WhereNotNull()
            .ToList()
        );

    public IReadOnlyList<VideoLocal> GetVideosWithMissingCrossReferenceData()
        => ReadLock(() => Cache.Values
            .Where(a =>
            {
                if (a.IsIgnored)
                    return false;

                var xrefs = RepoFactory.CrossRef_File_Episode.GetByEd2k(a.Hash);
                if (!xrefs.Any())
                    return false;

                return !xrefs.All(IsImported);
            })
            .OrderByNatural(local =>
            {
                var place = local?.FirstValidPlace;
                if (place == null) return null;
                return place.FullServerPath ?? place.FilePath;
            })
            .ThenBy(local => local?.VideoLocalID ?? 0)
            .WhereNotNull()
            .ToList()
        );

    private static bool IsImported(SVR_CrossRef_File_Episode xref)
    {
        if (xref.AnimeID == 0)
            return false;

        if (xref.ReleaseInfo is null)
            return false;

        var episode = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(xref.EpisodeID);
        if (episode?.AniDB_Episode == null)
            return false;

        var anime = RepoFactory.AnimeSeries.GetByAnimeID(xref.AnimeID);
        return anime?.AniDB_Anime != null;
    }

    public IReadOnlyList<VideoLocal> GetVideosWithoutEpisodeUnsorted()
        => ReadLock(() => Cache.Values
            .Where(a => !a.IsIgnored && !RepoFactory.CrossRef_File_Episode.GetByEd2k(a.Hash).Any())
            .ToList()
        );

    public IReadOnlyList<VideoLocal> GetManuallyLinkedVideos()
        => RepoFactory.CrossRef_File_Episode.GetAll()
                .Select(a => GetByEd2k(a.Hash))
                .WhereNotNull()
                .ToList();

    public IReadOnlyList<VideoLocal> GetIgnoredVideos()
        => ReadLock(() => _ignored!.GetMultiple(true));

    public VideoLocal? GetByMyListID(int myListID)
        => ReadLock(() => Cache.Values.FirstOrDefault(a => a.MyListID == myListID));
}
