using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FluentNHibernate.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Commons.Utils;
using Shoko.Models.MediaInfo;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.LZ4;
using Shoko.Server.Models;
using Shoko.Server.Server;
using Shoko.Server.Utilities.MediaInfoLib;

namespace Shoko.Server.Repositories.Cached;

public class VideoLocalRepository : BaseCachedRepository<SVR_VideoLocal, int>
{
    private PocoIndex<int, SVR_VideoLocal, string> _hashes;
    private PocoIndex<int, SVR_VideoLocal, string> _sha1;
    private PocoIndex<int, SVR_VideoLocal, string> _md5;
    private PocoIndex<int, SVR_VideoLocal, int> _ignored;

    public VideoLocalRepository()
    {
        DeleteWithOpenTransactionCallback = (ses, obj) =>
        {
            RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(ses, obj.Places.ToList());
            RepoFactory.VideoLocalUser.DeleteWithOpenTransaction(
                ses,
                RepoFactory.VideoLocalUser.GetByVideoLocalID(obj.VideoLocalID)
            );
        };
    }

    protected override int SelectKey(SVR_VideoLocal entity)
    {
        return entity.VideoLocalID;
    }

    public override void PopulateIndexes()
    {
        //Fix null hashes
        foreach (var l in Cache.Values)
        {
            if (l.MD5 == null || l.SHA1 == null || l.Hash == null || l.FileName == null)
            {
                l.MediaVersion = 0;
                if (l.MD5 == null)
                {
                    l.MD5 = string.Empty;
                }

                if (l.SHA1 == null)
                {
                    l.SHA1 = string.Empty;
                }

                if (l.Hash == null)
                {
                    l.Hash = string.Empty;
                }

                if (l.FileName == null)
                {
                    l.FileName = string.Empty;
                }
            }
        }

        _hashes = new PocoIndex<int, SVR_VideoLocal, string>(Cache, a => a.Hash);
        _sha1 = new PocoIndex<int, SVR_VideoLocal, string>(Cache, a => a.SHA1);
        _md5 = new PocoIndex<int, SVR_VideoLocal, string>(Cache, a => a.MD5);
        _ignored = new PocoIndex<int, SVR_VideoLocal, int>(Cache, a => a.IsIgnored);
    }

    public override void RegenerateDb()
    {
        ServerState.Instance.ServerStartingStatus = string.Format(
            Resources.Database_Validating, nameof(VideoLocal), " Checking Media Info"
        );
        var count = 0;
        int max;

        var list = Cache.Values.Where(a => a is { MediaVersion: 4, MediaBlob: { Length: > 0 } }).ToList();
        max = list.Count;

        foreach (var batch in list.Batch(50))
        {
            using var session2 = DatabaseFactory.SessionFactory.OpenSession();
            using var transaction = session2.BeginTransaction();
            foreach (var a in batch)
            {
                var media = CompressionHelper.DeserializeObject<MediaContainer>(a.MediaBlob, a.MediaSize,
                    new JsonConverter[] { new StreamJsonConverter() });
                a.Media = media;
                RepoFactory.VideoLocal.SaveWithOpenTransaction(session2, a);
                count++;
                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Validating, nameof(VideoLocal),
                    " Converting MediaInfo to MessagePack - " + count + "/" + max
                );
            }

            transaction.Commit();
        }

        count = 0;
        try
        {
            list = Cache.Values.Where(a =>
                    (a.MediaVersion < SVR_VideoLocal.MEDIA_VERSION &&
                     !(SVR_VideoLocal.MEDIA_VERSION == 5 && a.MediaVersion == 4)) || a.MediaBlob == null)
                .ToList();
            max = list.Count;

            var commandFactory = ShokoServer.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
            list.ForEach(
                a =>
                {
                    var cmd = commandFactory.Create<CommandRequest_ReadMediaInfo>(c => c.VideoLocalID = a.VideoLocalID);
                    cmd.Save();
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
        using var session = DatabaseFactory.SessionFactory.OpenSession();
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
            var froms = values.Except(to).ToList();
            foreach (var from in froms)
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

            toRemove.AddRange(froms);
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

        ServerState.Instance.ServerStartingStatus = string.Format(
            Resources.Database_Validating, nameof(VideoLocal),
            " Cleaning Fragmented Records"
        );
        using (var transaction = session.BeginTransaction())
        {
            var list2 = Cache.Values.SelectMany(a => RepoFactory.CrossRef_File_Episode.GetByHash(a.Hash))
                .Where(
                    a => RepoFactory.AniDB_Anime.GetByAnimeID(a.AnimeID) == null ||
                         a.GetEpisode() == null
                ).ToArray();
            count = 0;
            max = list2.Length;
            foreach (var xref in list2)
            {
                // We don't need to update anything since they don't exist
                RepoFactory.CrossRef_File_Episode.DeleteWithOpenTransaction(session, xref);
                count++;
                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Validating, nameof(VideoLocal),
                    " Cleaning Fragmented Records - " + count + "/" + max
                );
            }

            transaction.Commit();
        }
    }

    public List<SVR_VideoLocal> GetByImportFolder(int importFolderID)
    {
        return RepoFactory.VideoLocalPlace.GetByImportFolder(importFolderID)
            .Select(a => GetByID(a.VideoLocalID))
            .Where(a => a != null)
            .Distinct()
            .ToList();
    }

    private void UpdateMediaContracts(SVR_VideoLocal obj)
    {
        if (obj.Media != null && obj.MediaVersion >= SVR_VideoLocal.MEDIA_VERSION)
        {
            return;
        }

        var place = obj.GetBestVideoLocalPlace(true);
        place?.RefreshMediaInfo();
    }

    public override void Delete(SVR_VideoLocal obj)
    {
        var list = obj.GetAnimeEpisodes();
        base.Delete(obj);
        list.Where(a => a != null).ForEach(a => RepoFactory.AnimeEpisode.Save(a));
    }

    public override void Save(SVR_VideoLocal obj)
    {
        Save(obj, true);
    }

    public void Save(SVR_VideoLocal obj, bool updateEpisodes)
    {
        if (obj.VideoLocalID == 0)
        {
            obj.Media = null;
            base.Save(obj);
        }

        UpdateMediaContracts(obj);
        base.Save(obj);

        if (updateEpisodes)
        {
            RepoFactory.AnimeEpisode.Save(obj.GetAnimeEpisodes());
        }
    }

    public SVR_VideoLocal GetByHash(string hash)
    {
        return ReadLock(() => _hashes.GetOne(hash));
    }

    public SVR_VideoLocal GetByMD5(string hash)
    {
        return ReadLock(() => _md5.GetOne(hash));
    }

    public SVR_VideoLocal GetBySHA1(string hash)
    {
        return ReadLock(() => _sha1.GetOne(hash));
    }

    public SVR_VideoLocal GetByHashAndSize(string hash, long fsize)
    {
        return ReadLock(() => _hashes.GetMultiple(hash).FirstOrDefault(a => a.FileSize == fsize));
    }

    public List<SVR_VideoLocal> GetByName(string fileName)
    {
        return ReadLock(
            () => Cache.Values.Where(
                    p => p.Places.Any(
                        a => a.FilePath.FuzzyMatches(fileName)
                    )
                )
                .ToList()
        );
    }

    public List<SVR_VideoLocal> GetMostRecentlyAdded(int maxResults, int jmmuserID)
    {
        var user = RepoFactory.JMMUser.GetByID(jmmuserID);
        if (user == null)
        {
            return ReadLock(() =>
                maxResults == -1
                    ? Cache.Values.OrderByDescending(a => a.DateTimeCreated).ToList()
                    : Cache.Values.OrderByDescending(a => a.DateTimeCreated).Take(maxResults).ToList());
        }

        if (maxResults == -1)
        {
            return ReadLock(
                () => Cache.Values
                    .Where(
                        a => a.GetAnimeEpisodes().Select(b => b.GetAnimeSeries()).Where(b => b != null)
                            .DistinctBy(b => b.AniDB_ID).All(user.AllowedSeries)
                    ).OrderByDescending(a => a.DateTimeCreated)
                    .ToList()
            );
        }

        return ReadLock(
            () => Cache.Values
                .Where(a => a.GetAnimeEpisodes().Select(b => b.GetAnimeSeries()).Where(b => b != null)
                    .DistinctBy(b => b.AniDB_ID).All(user.AllowedSeries)).OrderByDescending(a => a.DateTimeCreated)
                .Take(maxResults).ToList()
        );
    }

    public List<SVR_VideoLocal> GetMostRecentlyAdded(int take, int skip, int jmmuserID)
    {
        if (skip < 0)
        {
            skip = 0;
        }

        if (take == 0)
        {
            return new List<SVR_VideoLocal>();
        }

        var user = jmmuserID == -1 ? null : RepoFactory.JMMUser.GetByID(jmmuserID);
        if (user == null)
        {
            return ReadLock(() =>
                take == -1
                    ? Cache.Values.OrderByDescending(a => a.DateTimeCreated).Skip(skip).ToList()
                    : Cache.Values.OrderByDescending(a => a.DateTimeCreated).Skip(skip).Take(take).ToList());
        }

        return ReadLock(
            () => take == -1
                ? Cache.Values
                    .Where(a => a.GetAnimeEpisodes().Select(b => b.GetAnimeSeries()).Where(b => b != null)
                        .DistinctBy(b => b.AniDB_ID).All(user.AllowedSeries))
                    .OrderByDescending(a => a.DateTimeCreated)
                    .Skip(skip)
                    .ToList()
                : Cache.Values
                    .Where(a => a.GetAnimeEpisodes().Select(b => b.GetAnimeSeries()).Where(b => b != null)
                        .DistinctBy(b => b.AniDB_ID).All(user.AllowedSeries))
                    .OrderByDescending(a => a.DateTimeCreated)
                    .Skip(skip)
                    .Take(take)
                    .ToList()
        );
    }

    public List<SVR_VideoLocal> GetRandomFiles(int maxResults)
    {
        var values = ReadLock(Cache.Values.ToList);

        using var en = new UniqueRandoms(0, values.Count - 1).GetEnumerator();
        var vids = new List<SVR_VideoLocal>();
        if (maxResults > values.Count)
        {
            maxResults = values.Count;
        }

        for (var x = 0; x < maxResults; x++)
        {
            en.MoveNext();
            vids.Add(values.ElementAt(en.Current));
        }

        return vids;
    }

    public class UniqueRandoms : IEnumerable<int>
    {
        private readonly Random _rand = new();
        private readonly List<int> _candidates;

        public UniqueRandoms(int minInclusive, int maxInclusive)
        {
            _candidates =
                Enumerable.Range(minInclusive, maxInclusive - minInclusive + 1).ToList();
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
        {
            return GetEnumerator();
        }
    }


    /// <summary>
    /// returns all the VideoLocal records associate with an AnimeEpisode Record
    /// </summary>
    /// <param name="episodeID"></param>
    /// <returns></returns>
    /// 
    public List<SVR_VideoLocal> GetByAniDBEpisodeID(int episodeID)
    {
        return RepoFactory.CrossRef_File_Episode.GetByEpisodeID(episodeID)
            .Select(a => GetByHash(a.Hash))
            .Where(a => a != null)
            .ToList();
    }


    public List<SVR_VideoLocal> GetMostRecentlyAddedForAnime(int maxResults, int animeID)
    {
        return
            RepoFactory.CrossRef_File_Episode.GetByAnimeID(animeID)
                .Select(a => GetByHash(a.Hash))
                .Where(a => a != null)
                .OrderByDescending(a => a.DateTimeCreated)
                .Take(maxResults)
                .ToList();
    }

    public List<SVR_VideoLocal> GetByInternalVersion(int iver)
    {
        return RepoFactory.AniDB_File.GetByInternalVersion(iver)
            .Select(a => GetByHash(a.Hash))
            .Where(a => a != null)
            .ToList();
    }

    /// <summary>
    /// returns all the VideoLocal records associate with an AniDB_Anime Record
    /// </summary>
    /// <param name="animeID"></param>
    /// <returns></returns>
    public List<SVR_VideoLocal> GetByAniDBAnimeID(int animeID)
    {
        return
            RepoFactory.CrossRef_File_Episode.GetByAnimeID(animeID)
                .Select(a => GetByHash(a.Hash))
                .Where(a => a != null)
                .ToList();
    }

    public List<SVR_VideoLocal> GetVideosWithoutHash()
    {
        return ReadLock(() => _hashes.GetMultiple(""));
    }

    public List<SVR_VideoLocal> GetVideosWithoutEpisode()
    {
        return ReadLock(
            () => Cache.Values
                .Where(
                    a =>
                    {
                        if (a.IsIgnored != 0) return false;

                        var xrefs = RepoFactory.CrossRef_File_Episode.GetByHash(a.Hash);
                        return !xrefs.Any() || xrefs.All(xref => RepoFactory.AniDB_Episode.GetByEpisodeID(xref.EpisodeID) == null || RepoFactory.AniDB_Anime.GetByAnimeID(xref.AnimeID) == null);
                    }
                )
                .OrderByNatural(local =>
                {
                    var place = local?.GetBestVideoLocalPlace();
                    if (place == null) return null;
                    return place.FullServerPath ?? place.FilePath;
                })
                .ThenBy(local => local?.VideoLocalID ?? 0)
                .ToList()
        );
    }

    public List<SVR_VideoLocal> GetVideosWithoutEpisodeUnsorted()
    {
        return ReadLock(() =>
            Cache.Values.Where(a => a.IsIgnored == 0 && !RepoFactory.CrossRef_File_Episode.GetByHash(a.Hash).Any())
                .ToList());
    }

    public List<SVR_VideoLocal> GetManuallyLinkedVideos()
    {
        return
            RepoFactory.CrossRef_File_Episode.GetAll()
                .Where(a => a.CrossRefSource != 1)
                .Select(a => GetByHash(a.Hash))
                .Where(a => a != null)
                .ToList();
    }

    public List<SVR_VideoLocal> GetExactDuplicateVideos()
    {
        return
            RepoFactory.VideoLocalPlace.GetAll()
                .GroupBy(a => a.VideoLocalID)
                .Select(a => a.ToArray())
                .Where(a => a.Length > 1)
                .Select(a => GetByID(a[0].VideoLocalID))
                .Where(a => a != null)
                .ToList();
    }

    public List<SVR_VideoLocal> GetIgnoredVideos()
    {
        return ReadLock(() => _ignored.GetMultiple(1));
    }

    public SVR_VideoLocal GetByMyListID(int myListID)
    {
        return ReadLock(() => Cache.Values.FirstOrDefault(a => a.MyListID == myListID));
    }
}
