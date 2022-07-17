using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentNHibernate.Utils;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Commons.Utils;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Cached
{
    public class VideoLocalRepository : BaseCachedRepository<SVR_VideoLocal, int>
    {
        private readonly Logger logger = LogManager.GetCurrentClassLogger();
        private PocoIndex<int, SVR_VideoLocal, string> Hashes;
        private PocoIndex<int, SVR_VideoLocal, string> SHA1;
        private PocoIndex<int, SVR_VideoLocal, string> MD5;
        private PocoIndex<int, SVR_VideoLocal, int> Ignored;

        public VideoLocalRepository()
        {
            DeleteWithOpenTransactionCallback = (ses, obj) =>
            {
                RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(ses, obj.Places.ToList());
                RepoFactory.VideoLocalUser.DeleteWithOpenTransaction(ses,
                    RepoFactory.VideoLocalUser.GetByVideoLocalID(obj.VideoLocalID));
            };
        }

        protected override int SelectKey(SVR_VideoLocal entity)
        {
            return entity.VideoLocalID;
        }

        public override void PopulateIndexes()
        {
            //Fix null hashes
            foreach (SVR_VideoLocal l in Cache.Values)
                if (l.MD5 == null || l.SHA1 == null || l.Hash == null || l.FileName == null)
                {
                    l.MediaVersion = 0;
                    if (l.MD5 == null)
                        l.MD5 = string.Empty;
                    if (l.SHA1 == null)
                        l.SHA1 = string.Empty;
                    if (l.Hash == null)
                        l.Hash = string.Empty;
                    if (l.FileName == null)
                        l.FileName = string.Empty;
                }
            Hashes = new PocoIndex<int, SVR_VideoLocal, string>(Cache, a => a.Hash);
            SHA1 = new PocoIndex<int, SVR_VideoLocal, string>(Cache, a => a.SHA1);
            MD5 = new PocoIndex<int, SVR_VideoLocal, string>(Cache, a => a.MD5);
            Ignored = new PocoIndex<int, SVR_VideoLocal, int>(Cache, a => a.IsIgnored);
        }

        public override void RegenerateDb()
        {
            ServerState.Instance.ServerStartingStatus = string.Format(
                Resources.Database_Validating, nameof(VideoLocal), " Checking Media Info");
            int count = 0;
            int max;
            try
            {
                var list = Cache.Values.Where(a => a.MediaVersion < SVR_VideoLocal.MEDIA_VERSION || a.MediaBlob == null)
                    .ToList();
                max = list.Count;

                list.ForEach(
                    a =>
                    {
                        CommandRequest_ReadMediaInfo cmd = new CommandRequest_ReadMediaInfo(a.VideoLocalID);
                        cmd.Save();
                        count++;
                        ServerState.Instance.ServerStartingStatus = string.Format(
                            Resources.Database_Validating, nameof(VideoLocal),
                            " Queuing Media Info Commands - " + count + "/" + max);
                    });
            }
            catch
            {
                // ignore
            }

            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                Dictionary<string, List<SVR_VideoLocal>> locals = Cache.Values
                    .Where(a => !string.IsNullOrWhiteSpace(a.Hash))
                    .GroupBy(a => a.Hash)
                    .ToDictionary(g => g.Key, g => g.ToList());
                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Validating, nameof(VideoLocal),
                    " Cleaning Empty Records");
                using (var transaction = session.BeginTransaction())
                {
                    var list = Cache.Values.Where(a => a.IsEmpty()).ToList();
                    count = 0;
                    max = list.Count;
                    foreach (SVR_VideoLocal remove in list)
                    {
                        RepoFactory.VideoLocal.DeleteWithOpenTransaction(session, remove);
                        count++;
                        ServerState.Instance.ServerStartingStatus = string.Format(
                            Resources.Database_Validating, nameof(VideoLocal),
                            " Cleaning Empty Records - " + count + "/" + max);
                    }
                    transaction.Commit();
                }
                var toRemove = new List<SVR_VideoLocal>();
                var comparer = new VideoLocalComparer();

                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Validating, nameof(VideoLocal),
                    " Checking for Duplicate Records");

                foreach (string hash in locals.Keys)
                {
                    List<SVR_VideoLocal> values = locals[hash];
                    values.Sort(comparer);
                    SVR_VideoLocal to = values.First();
                    List<SVR_VideoLocal> froms = values.Except(to).ToList();
                    foreach (SVR_VideoLocal from in froms)
                    {
                        List<SVR_VideoLocal_Place> places = from.Places;
                        if (places == null || places.Count == 0) continue;
                        using (var transaction = session.BeginTransaction())
                        {
                            foreach (SVR_VideoLocal_Place place in places)
                            {
                                place.VideoLocalID = to.VideoLocalID;
                                RepoFactory.VideoLocalPlace.SaveWithOpenTransaction(session, place);
                            }
                            transaction.Commit();
                        }
                    }
                    toRemove.AddRange(froms);
                }

                count = 0;
                max = toRemove.Count;
                foreach (SVR_VideoLocal[] batch in toRemove.Batch(50))
                {
                    using (var transaction = session.BeginTransaction())
                    {
                        foreach (SVR_VideoLocal remove in batch)
                        {
                            count++;
                            ServerState.Instance.ServerStartingStatus = string.Format(
                                Resources.Database_Validating, nameof(VideoLocal),
                                " Cleaning Duplicate Records - " + count + "/" + max);
                            DeleteWithOpenTransaction(session, remove);
                        }
                        transaction.Commit();
                    }
                }

                ServerState.Instance.ServerStartingStatus = string.Format(
                    Resources.Database_Validating, nameof(VideoLocal),
                    " Cleaning Fragmented Records");
                using (var transaction = session.BeginTransaction())
                {
                    var list = Cache.Values.SelectMany(a => RepoFactory.CrossRef_File_Episode.GetByHash(a.Hash))
                        .Where(a => RepoFactory.AniDB_Anime.GetByAnimeID(a.AnimeID) == null ||
                                    a.GetEpisode() == null).ToArray();
                    count = 0;
                    max = list.Length;
                    foreach (var xref in list)
                    {
                        // We don't need to update anything since they don't exist
                        RepoFactory.CrossRef_File_Episode.DeleteWithOpenTransaction(session, xref);
                        count++;
                        ServerState.Instance.ServerStartingStatus = string.Format(
                            Resources.Database_Validating, nameof(VideoLocal),
                            " Cleaning Fragmented Records - " + count + "/" + max);
                    }
                    transaction.Commit();
                }
            }
        }

        public List<SVR_VideoLocal> GetByImportFolder(int importFolderID)
        {
            return RepoFactory.VideoLocalPlace.GetByImportFolder(importFolderID)
                .Select(a => a.VideoLocal)
                .Where(a => a != null)
                .Distinct()
                .ToList();
        }

        private void UpdateMediaContracts(SVR_VideoLocal obj)
        {
            if (obj.Media == null || obj.MediaVersion < SVR_VideoLocal.MEDIA_VERSION)
            {
                SVR_VideoLocal_Place place = obj.GetBestVideoLocalPlace(true);
                place?.RefreshMediaInfo();
            }
        }

        public override void Delete(SVR_VideoLocal obj)
        {
            List<SVR_AnimeEpisode> list = obj.GetAnimeEpisodes();
            base.Delete(obj);
            list.Where(a => a != null).ForEach(a => RepoFactory.AnimeEpisode.Save(a));
        }

        public override void Save(SVR_VideoLocal obj)
        {
            Save(obj, true);
        }

        public void Save(SVR_VideoLocal obj, bool updateEpisodes)
        {
            lock (obj)
            {
                if (obj.VideoLocalID == 0)
                {
                    obj.Media = null;
                    base.Save(obj);
                }
                UpdateMediaContracts(obj);
                base.Save(obj);
            }
            if (updateEpisodes)
                RepoFactory.AnimeEpisode.Save(obj.GetAnimeEpisodes());
        }


        public SVR_VideoLocal GetByHash(string hash)
        {
            lock (Cache)
            {
                return Hashes.GetOne(hash);
            }
        }

        public SVR_VideoLocal GetByMD5(string hash)
        {
            lock (Cache)
            {
                return MD5.GetOne(hash);
            }
        }

        public SVR_VideoLocal GetBySHA1(string hash)
        {
            lock (Cache)
            {
                return SHA1.GetOne(hash);
            }
        }

        public long GetTotalRecordCount()
        {
            lock (Cache)
            {
                return Cache.Keys.Count;
            }
        }

        public SVR_VideoLocal GetByHashAndSize(string hash, long fsize)
        {
            lock (Cache)
            {
                return Hashes.GetMultiple(hash).FirstOrDefault(a => a.FileSize == fsize);
            }
        }

        public List<SVR_VideoLocal> GetByName(string fileName)
        {
            lock (Cache)
            {
                return Cache.Values.Where(p => p.Places.Any(
                        a => a.FilePath.FuzzyMatches(fileName)))
                    .ToList();
            }
        }

        public List<SVR_VideoLocal> GetMostRecentlyAdded(int maxResults, int jmmuserID)
        {
            SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
            if (user == null)
            {
                lock (Cache)
                {
                    if (maxResults == -1)
                        return Cache.Values.OrderByDescending(a => a.DateTimeCreated).ToList();
                    return Cache.Values.OrderByDescending(a => a.DateTimeCreated).Take(maxResults).ToList();
                }
            }

            if (maxResults == -1)
                return Cache.Values
                    .Where(a => a.GetAnimeEpisodes().Select(b => b.GetAnimeSeries()).Where(b => b != null)
                        .DistinctBy(b => b.AniDB_ID).All(user.AllowedSeries)).OrderByDescending(a => a.DateTimeCreated)
                    .ToList();
            return Cache.Values
                .Where(a => a.GetAnimeEpisodes().Select(b => b.GetAnimeSeries()).Where(b => b != null)
                    .DistinctBy(b => b.AniDB_ID).All(user.AllowedSeries)).OrderByDescending(a => a.DateTimeCreated)
                .Take(maxResults).ToList();
        }

        public List<SVR_VideoLocal> GetMostRecentlyAdded(int take, int skip, int jmmuserID = -1)
        {
            if (skip < 0) skip = 0;
            if (take == 0) return new List<SVR_VideoLocal>();
            
            SVR_JMMUser user = jmmuserID == -1 ? null : RepoFactory.JMMUser.GetByID(jmmuserID);
            if (user == null)
            {
                lock (Cache)
                {
                    if (take == -1)
                        return Cache.Values.OrderByDescending(a => a.DateTimeCreated).Skip(skip).ToList();
                    return Cache.Values.OrderByDescending(a => a.DateTimeCreated).Skip(skip).Take(take).ToList();
                }
            }
            
            if (take == -1)
                return Cache.Values
                    .Where(a => a.GetAnimeEpisodes().Select(b => b.GetAnimeSeries()).Where(b => b != null).DistinctBy(b => b.AniDB_ID).All(user.AllowedSeries))
                    .OrderByDescending(a => a.DateTimeCreated)
                    .Skip(skip)
                    .ToList();

            return Cache.Values
                .Where(a => a.GetAnimeEpisodes().Select(b => b.GetAnimeSeries()).Where(b => b != null).DistinctBy(b => b.AniDB_ID).All(user.AllowedSeries))
                .OrderByDescending(a => a.DateTimeCreated)
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        public List<SVR_VideoLocal> GetRandomFiles(int maxResults)
        {
            lock (Cache)
            {
                IEnumerator<int> en = new UniqueRandoms(0, Cache.Values.Count - 1).GetEnumerator();
                List<SVR_VideoLocal> vids = new List<SVR_VideoLocal>();
                if (maxResults > Cache.Values.Count)
                    maxResults = Cache.Values.Count;
                for (int x = 0; x < maxResults; x++)
                {
                    en.MoveNext();
                    vids.Add(Cache.Values.ElementAt(en.Current));
                }
                en.Dispose();
                return vids;
            }
        }

        public class UniqueRandoms : IEnumerable<int>
        {
            private readonly Random _rand = new Random();
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
                    int index = _rand.Next(_candidates.Count);
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


        public List<SVR_VideoLocal> GetByAniDBResolution(string res)
        {
            return RepoFactory.AniDB_File.GetByResolution(res)
                .Select(a => GetByHash(a.Hash))
                .Where(a => a != null)
                .ToList();
        }

        public List<SVR_VideoLocal> GetByInternalVersion(int iver)
        {
            return RepoFactory.AniDB_File.GetByInternalVersion(iver)
                .Select(a => GetByHash(a.Hash))
                .Where(a => a != null)
                .ToList();
        }

        public List<SVR_VideoLocal> GetWithMissingChapters()
        {
            return RepoFactory.AniDB_File.GetWithWithMissingChapters()
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
            lock (Cache)
            {
                return Hashes.GetMultiple("").ToList();
            }
        }

        public List<SVR_VideoLocal> GetVideosWithoutVideoInfo()
        {
            lock (Cache)
            {
                return Cache.Values.Where(a => a.Media == null || a.MediaVersion < SVR_VideoLocal.MEDIA_VERSION)
                    .ToList();
            }
        }

        public List<SVR_VideoLocal> GetVideosWithoutEpisode()
        {
            lock (Cache)
            {
                return Cache.Values
                    .Where(a =>
                    {
                        if (a.IsIgnored != 0) return false;
                        var xrefs = RepoFactory.CrossRef_File_Episode.GetByHash(a.Hash);
                        if (!xrefs.Any()) return true;
                        return RepoFactory.AniDB_Anime.GetByAnimeID(xrefs.FirstOrDefault().AnimeID) == null;
                    })
                    .OrderByNatural(local => local?.GetBestVideoLocalPlace()?.FilePath)
                    .ThenBy(local => local?.VideoLocalID ?? 0)
                    .ToList();
            }
        }

        public IEnumerable<SVR_VideoLocal> GetVideosWithoutEpisodeUnsorted()
        {
            lock (Cache)
            {
                return Cache.Values
                    .Where(a => a.IsIgnored == 0 && !RepoFactory.CrossRef_File_Episode.GetByHash(a.Hash).Any());
            }
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
                    .Select(a => GetByID(a.FirstOrDefault().VideoLocalID))
                    .Where(a => a != null)
                    .ToList();
        }

        public List<SVR_VideoLocal> GetIgnoredVideos()
        {
            lock (Cache)
            {
                return Ignored.GetMultiple(1);
            }
        }

        public SVR_VideoLocal GetByMyListID(int myListID)
        {
            lock (Cache)
                return Cache.Values.FirstOrDefault(a => a.MyListID == myListID);
        }
    }
}
