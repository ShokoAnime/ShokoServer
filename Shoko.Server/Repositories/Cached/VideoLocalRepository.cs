using System;
using System.Collections.Generic;
using System.Linq;
using FluentNHibernate.Utils;
using Shoko.Models.Server;
using NHibernate.Util;
using NLog;
using NutzCode.InMemoryIndex;
using Pri.LongPath;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Extensions;

namespace Shoko.Server.Repositories.Cached
{
    public class VideoLocalRepository : BaseCachedRepository<SVR_VideoLocal, int>
    {
        private readonly Logger logger = LogManager.GetCurrentClassLogger();
        private PocoIndex<int, SVR_VideoLocal, string> Hashes;
        private PocoIndex<int, SVR_VideoLocal, string> SHA1;
        private PocoIndex<int, SVR_VideoLocal, string> MD5;
        private PocoIndex<int, SVR_VideoLocal, int> Ignored;

        private VideoLocalRepository()
        {
            DeleteWithOpenTransactionCallback = (ses, obj) =>
            {
                RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(ses, obj.Places.ToList());
                RepoFactory.VideoLocalUser.DeleteWithOpenTransaction(ses,
                    RepoFactory.VideoLocalUser.GetByVideoLocalID(obj.VideoLocalID));
            };
        }

        public static VideoLocalRepository Create()
        {
            return new VideoLocalRepository();
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
            int count = 0;
            int max = 0;
            ServerState.Instance.CurrentSetupStatus = string.Format(
                Commons.Properties.Resources.Database_Cache, typeof(VideoLocal).Name, " Generating Media Info");
            try
            {
                count = 0;
                var list = Cache.Values.Where(a => a.MediaVersion < SVR_VideoLocal.MEDIA_VERSION || a.MediaBlob == null).ToList();
                max = list.Count;

                list.ForEach(
                        a =>
                        {
                            //Fix possible paths in filename
                            if (!string.IsNullOrEmpty(a.FileName))
                            {
                                int b = a.FileName.LastIndexOf($"{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
                                if (b > 0)
                                    a.FileName = a.FileName.Substring(b + 1);
                            }
                            Save(a, false);
                            count++;
                            ServerState.Instance.CurrentSetupStatus = string.Format(
                                Commons.Properties.Resources.Database_Cache, typeof(VideoLocal).Name,
                                " Generating Media Info - " + count + "/" + max);
                        });
            }
            catch
            {
                // ignore
            }
            //Fix possible paths in filename
            try
            {
                ServerState.Instance.CurrentSetupStatus = string.Format(
                    Commons.Properties.Resources.Database_Cache, typeof(VideoLocal).Name, " Cleaning File Paths");
                var list = Cache.Values.Where(a => a.FileName.Contains("\\")).ToList();
                count = 0;
                max = list.Count;
                list.ForEach(a =>
                {
                    try
                    {
                        int b = a.FileName.LastIndexOf($"{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
                        a.FileName = a.FileName.Substring(b + 1);
                        Save(a, false);
                        count++;
                        ServerState.Instance.CurrentSetupStatus = string.Format(
                            Commons.Properties.Resources.Database_Cache, typeof(VideoLocal).Name,
                            " Cleaning File Paths - " + count + "/" + max);
                        }
                    catch(Exception e)
                    {
                        logger.Error($"Error cleaning path on file: {a.FileName}\r\n{e}");
                    }
                });
            }
            catch
            {
                // ignore
            }

            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                Dictionary<string, List<SVR_VideoLocal>> locals = Cache.Values.Where(a => !string.IsNullOrWhiteSpace(a.Hash))
                    .GroupBy(a => a.Hash)
                    .ToDictionary(g => g.Key, g => g.ToList());
                ServerState.Instance.CurrentSetupStatus = string.Format(
                    Commons.Properties.Resources.Database_Cache, typeof(VideoLocal).Name, " Cleaning Empty Records");
                using (var transaction = session.BeginTransaction())
                {
                    var list = Cache.Values.Where(a => a.IsEmpty()).ToList();
                    count = 0;
                    max = list.Count;
                    foreach (SVR_VideoLocal remove in list)
                    {
                        RepoFactory.VideoLocal.DeleteWithOpenTransaction(session, remove);
                        count++;
                        ServerState.Instance.CurrentSetupStatus = string.Format(
                            Commons.Properties.Resources.Database_Cache, typeof(VideoLocal).Name,
                            " Cleaning Empty Records - " + count + "/" + max);
                    }
                    transaction.Commit();
                }
                var toRemove = new List<SVR_VideoLocal>();
                var comparer = new VideoLocalComparer();
                count = 0;
                max = locals.Keys.Count;

                ServerState.Instance.CurrentSetupStatus = string.Format(
                    Commons.Properties.Resources.Database_Cache, typeof(VideoLocal).Name, " Cleaning Duplicate Records");

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
                    count++;
                    ServerState.Instance.CurrentSetupStatus = string.Format(
                        Commons.Properties.Resources.Database_Cache, typeof(VideoLocal).Name,
                        " Cleaning Duplicate Records - " + count + "/" + max);
                }

                using (var transaction = session.BeginTransaction())
                {
                    foreach (SVR_VideoLocal remove in toRemove)
                        DeleteWithOpenTransaction(session, remove);
                    transaction.Commit();
                }

                ServerState.Instance.CurrentSetupStatus = string.Format(
                    Commons.Properties.Resources.Database_Cache, typeof(VideoLocal).Name, " Cleaning Fragmented Records");
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
                        ServerState.Instance.CurrentSetupStatus = string.Format(
                            Commons.Properties.Resources.Database_Cache, typeof(VideoLocal).Name,
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
            if (obj.Media == null || obj.MediaVersion < SVR_VideoLocal.MEDIA_VERSION || obj.Duration == 0)
            {
                SVR_VideoLocal_Place place = obj.GetBestVideoLocalPlace();
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
                        a => a.FilePath.Contains(fileName, StringComparison.InvariantCultureIgnoreCase)))
                    .ToList();
            }
        }

        public List<SVR_VideoLocal> GetMostRecentlyAdded(int maxResults)
        {
            lock (Cache)
            {
                if (maxResults == -1)
                    return Cache.Values.OrderByDescending(a => a.DateTimeCreated).ToList();
                return Cache.Values.OrderByDescending(a => a.DateTimeCreated).Take(maxResults).ToList();
            }
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

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
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
                return Cache.Values.Where(a => a.Media == null || a.MediaVersion < SVR_VideoLocal.MEDIA_VERSION ||
                                               a.Duration == 0)
                    .ToList();
            }
        }

        public List<SVR_VideoLocal> GetVideosWithoutEpisode()
        {
            lock (Cache)
            {
                HashSet<string> hashes = new HashSet<string>(RepoFactory.CrossRef_File_Episode.GetAll()
                    .Select(a => a.Hash));
                HashSet<string> vlocals = new HashSet<string>(Cache.Values.Where(a => a.IsIgnored == 0)
                    .Select(a => a.Hash));
                return vlocals.Except(hashes)
                    .SelectMany(a => Hashes.GetMultiple(a))
                    .OrderBy(a => a.DateTimeCreated)
                    .ToList();
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

        public List<SVR_VideoLocal> GetIgnoredVideos()
        {
            lock (Cache)
            {
                return Ignored.GetMultiple(1);
            }
        }
    }
}