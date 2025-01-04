using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FluentNHibernate.Utils;
using Microsoft.Extensions.DependencyInjection;
using NutzCode.InMemoryIndex;
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Exceptions;
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

public class VideoLocalRepository : BaseCachedRepository<SVR_VideoLocal, int>
{
    private PocoIndex<int, SVR_VideoLocal, string>? _ed2k;

    private PocoIndex<int, SVR_VideoLocal, string>? _sha1;

    private PocoIndex<int, SVR_VideoLocal, string>? _md5;

    private PocoIndex<int, SVR_VideoLocal, string>? _crc32;

    private PocoIndex<int, SVR_VideoLocal, bool>? _ignored;

    public VideoLocalRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        DeleteWithOpenTransactionCallback = (ses, obj) =>
        {
            RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(ses, obj.Places.ToList());
            RepoFactory.VideoLocalUser.DeleteWithOpenTransaction(ses, RepoFactory.VideoLocalUser.GetByVideoLocalID(obj.VideoLocalID));
        };
    }

    protected override int SelectKey(SVR_VideoLocal entity)
        => entity.VideoLocalID;

    public override void PopulateIndexes()
    {
        //Fix null hashes
        foreach (var l in Cache.Values)
        {
            if (l.MD5 != null && l.SHA1 != null && l.Hash != null && l.CRC32 != null && l.FileName != null) continue;

            l.MediaVersion = 0;
            l.MD5 ??= string.Empty;
            l.CRC32 ??= string.Empty;
            l.SHA1 ??= string.Empty;
            l.Hash ??= string.Empty;
            l.FileName ??= string.Empty;
        }

        _ed2k = new PocoIndex<int, SVR_VideoLocal, string>(Cache, a => a.Hash);
        _sha1 = new PocoIndex<int, SVR_VideoLocal, string>(Cache, a => a.SHA1);
        _md5 = new PocoIndex<int, SVR_VideoLocal, string>(Cache, a => a.MD5);
        _crc32 = new PocoIndex<int, SVR_VideoLocal, string>(Cache, a => a.CRC32);
        _ignored = new PocoIndex<int, SVR_VideoLocal, bool>(Cache, a => a.IsIgnored);
    }

    public override void RegenerateDb()
    {
        ServerState.Instance.ServerStartingStatus = string.Format(
            Resources.Database_Validating, nameof(VideoLocal), " Checking Media Info"
        );
        var count = 0;
        int max;
        IReadOnlyList<SVR_VideoLocal> list;

        try
        {
            list = Cache.Values.Where(a => a.MediaVersion < SVR_VideoLocal.MEDIA_VERSION || a.MediaInfo == null).ToList();
            max = list.Count;

            var scheduler = Utils.ServiceContainer.GetRequiredService<ISchedulerFactory>().GetScheduler().Result;
            list.ForEach(
                a =>
                {
                    scheduler.StartJob<MediaInfoJob>(c => c.VideoLocalID = a.VideoLocalID).GetAwaiter().GetResult();
                    count++;
                    ServerState.Instance.ServerStartingStatus = string.Format(
                        Resources.Database_Validating, nameof(VideoLocal),
                        " Queuing Media Info Commands - " + count + "/" + max
                    );
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
        ServerState.Instance.ServerStartingStatus = string.Format(
            Resources.Database_Validating, nameof(VideoLocal),
            " Cleaning Empty Records"
        );
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
                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Validating, nameof(VideoLocal),
                    " Cleaning Empty Records - " + count + "/" + max
                );
            }

            transaction.Commit();
        }

        var toRemove = new List<SVR_VideoLocal>();
        var comparer = new VideoLocalComparer();

        ServerState.Instance.ServerStartingStatus = string.Format(
            Resources.Database_Validating, nameof(VideoLocal),
            " Checking for Duplicate Records"
        );

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
                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Validating, nameof(VideoLocal),
                    " Cleaning Duplicate Records - " + count + "/" + max
                );
                DeleteWithOpenTransaction(session, remove);
            }

            transaction.Commit();
        }
    }

    public IReadOnlyList<SVR_VideoLocal> GetByImportFolder(int importFolderID)
        => RepoFactory.VideoLocalPlace.GetByImportFolder(importFolderID)
            .Select(a => GetByID(a.VideoLocalID))
            .WhereNotNull()
            .Distinct()
            .ToList();

    public override void Delete(SVR_VideoLocal obj)
    {
        var list = obj.AnimeEpisodes;
        base.Delete(obj);
        list.WhereNotNull().ForEach(a => RepoFactory.AnimeEpisode.Save(a));
    }

    public override void Save(SVR_VideoLocal obj)
    {
        Save(obj, true);
    }

    public void Save(SVR_VideoLocal obj, bool updateEpisodes)
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

    private static void UpdateMediaContracts(SVR_VideoLocal obj)
    {
        if (obj.MediaInfo != null && obj.MediaVersion >= SVR_VideoLocal.MEDIA_VERSION)
        {
            return;
        }

        var place = obj.FirstResolvedPlace;
        if (place != null) Utils.ServiceContainer.GetRequiredService<VideoLocal_PlaceService>().RefreshMediaInfo(place);
    }

    public SVR_VideoLocal? GetByEd2k(string hash)
    {
        if (string.IsNullOrEmpty(hash))
            throw new InvalidStateException("Trying to lookup a VideoLocal by an empty Hash");

        return ReadLock(() => _ed2k!.GetOne(hash));
    }

    public SVR_VideoLocal? GetByEd2kAndSize(string hash, long fileSize)
    {
        if (string.IsNullOrEmpty(hash))
            throw new InvalidStateException("Trying to lookup a VideoLocal by an empty Hash");

        if (fileSize <= 0)
            throw new InvalidStateException("Trying to lookup a VideoLocal by a filesize of 0");

        return ReadLock(() => _ed2k!.GetMultiple(hash).FirstOrDefault(a => a.FileSize == fileSize));
    }

    public SVR_VideoLocal? GetByMd5(string hash)
    {
        if (string.IsNullOrEmpty(hash))
            throw new InvalidStateException("Trying to lookup a VideoLocal by an empty MD5");

        return ReadLock(() => _md5!.GetOne(hash));
    }

    public SVR_VideoLocal? GetByMd5AndSize(string hash, long fileSize)
    {
        if (string.IsNullOrEmpty(hash))
            throw new InvalidStateException("Trying to lookup a VideoLocal by an empty MD5");

        if (fileSize <= 0)
            throw new InvalidStateException("Trying to lookup a VideoLocal by a filesize of 0");

        return ReadLock(() => _md5!.GetMultiple(hash).FirstOrDefault(a => a.FileSize == fileSize));
    }

    public SVR_VideoLocal? GetBySha1(string hash)
    {
        if (string.IsNullOrEmpty(hash))
            throw new InvalidStateException("Trying to lookup a VideoLocal by an empty SHA1");

        return ReadLock(() => _sha1!.GetOne(hash));
    }

    public SVR_VideoLocal? GetBySha1AndSize(string hash, long fileSize)
    {
        if (string.IsNullOrEmpty(hash))
            throw new InvalidStateException("Trying to lookup a VideoLocal by an empty SHA1");

        if (fileSize <= 0)
            throw new InvalidStateException("Trying to lookup a VideoLocal by a filesize of 0");

        return ReadLock(() => _sha1!.GetMultiple(hash).FirstOrDefault(a => a.FileSize == fileSize));
    }

    public SVR_VideoLocal? GetByCrc32(string hash)
    {
        if (string.IsNullOrEmpty(hash))
            throw new InvalidStateException("Trying to lookup a VideoLocal by an empty CRC32");

        return ReadLock(() => _crc32!.GetOne(hash));
    }

    public SVR_VideoLocal? GetByCrc32AndSize(string hash, long fileSize)
    {
        if (string.IsNullOrEmpty(hash))
            throw new InvalidStateException("Trying to lookup a VideoLocal by an empty CRC32");

        if (fileSize <= 0)
            throw new InvalidStateException("Trying to lookup a VideoLocal by a filesize of 0");

        return ReadLock(() => _crc32!.GetMultiple(hash).FirstOrDefault(a => a.FileSize == fileSize));
    }

    public IReadOnlyList<SVR_VideoLocal> GetByName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            throw new InvalidStateException("Trying to lookup a VideoLocal by an empty Filename");

        return ReadLock(() => Cache.Values
            .Where(p => p.Places.Any(a => a.FilePath.FuzzyMatch(fileName)))
            .ToList()
        );
    }

    public IReadOnlyList<SVR_VideoLocal> GetMostRecentlyAdded(int maxResults, int userID)
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

    public IReadOnlyList<SVR_VideoLocal> GetMostRecentlyAdded(int take, int skip, int userID)
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

    public IReadOnlyList<SVR_VideoLocal> GetRandomFiles(int maxResults)
    {
        var values = ReadLock(Cache.Values.ToList).Where(a => a.EpisodeCrossReferences.Any()).ToList();

        using var en = new UniqueRandoms(0, values.Count - 1).GetEnumerator();
        var list = new List<SVR_VideoLocal>();
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
    public IReadOnlyList<SVR_VideoLocal> GetByAniDBEpisodeID(int episodeID)
        => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(episodeID)
            .Select(a => GetByEd2k(a.Hash))
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<SVR_VideoLocal> GetMostRecentlyAddedForAnime(int maxResults, int animeID)
        => RepoFactory.CrossRef_File_Episode.GetByAnimeID(animeID)
                .Select(a => GetByEd2k(a.Hash))
                .WhereNotNull()
                .OrderByDescending(a => a.DateTimeCreated)
                .Take(maxResults)
                .ToList();

    public IReadOnlyList<SVR_VideoLocal> GetByInternalVersion(int internalVersion)
        => RepoFactory.AniDB_File.GetByInternalVersion(internalVersion)
            .Select(a => GetByEd2k(a.Hash))
            .WhereNotNull()
            .ToList();

    /// <summary>
    /// returns all the VideoLocal records associate with an AniDB_Anime Record
    /// </summary>
    /// <param name="animeID">AniDB Anime ID</param>
    /// <param name="xrefSource">Include to select only files from the selected
    /// cross-reference source.</param>
    /// <returns></returns>
    public IReadOnlyList<SVR_VideoLocal> GetByAniDBAnimeID(int animeID, CrossRefSource? xrefSource = null)
        => RepoFactory.CrossRef_File_Episode.GetByAnimeID(animeID)
            .Where(xref => !xrefSource.HasValue || xref.CrossRefSource != (int)xrefSource.Value)
            .Select(xref => GetByEd2k(xref.Hash))
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<SVR_VideoLocal> GetVideosWithoutHash()
        => ReadLock(() => _ed2k!.GetMultiple(""));

    public IReadOnlyList<SVR_VideoLocal> GetVideosWithoutEpisode(bool includeBrokenXRefs = false)
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

    public IReadOnlyList<SVR_VideoLocal> GetVideosWithMissingCrossReferenceData()
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

        if (xref.CrossRefSource == (int)CrossRefSource.AniDB)
        {
            var anidbFile = RepoFactory.AniDB_File.GetByHash(xref.Hash);
            if (anidbFile == null) return false;
        }

        var episode = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(xref.EpisodeID);
        if (episode?.AniDB_Episode == null)
            return false;

        var anime = RepoFactory.AnimeSeries.GetByAnimeID(xref.AnimeID);
        return anime?.AniDB_Anime != null;
    }

    public IReadOnlyList<SVR_VideoLocal> GetVideosWithoutEpisodeUnsorted()
        => ReadLock(() => Cache.Values
            .Where(a => !a.IsIgnored && !RepoFactory.CrossRef_File_Episode.GetByEd2k(a.Hash).Any())
            .ToList()
        );

    public IReadOnlyList<SVR_VideoLocal> GetManuallyLinkedVideos()
        => RepoFactory.CrossRef_File_Episode.GetAll()
                .Where(a => a.CrossRefSource != 1)
                .Select(a => GetByEd2k(a.Hash))
                .WhereNotNull()
                .ToList();

    public IReadOnlyList<SVR_VideoLocal> GetIgnoredVideos()
        => ReadLock(() => _ignored!.GetMultiple(true));

    public SVR_VideoLocal? GetByMyListID(int myListID)
        => ReadLock(() => Cache.Values.FirstOrDefault(a => a.MyListID == myListID));
}
