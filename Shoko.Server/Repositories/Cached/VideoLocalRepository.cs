using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using FluentNHibernate.Utils;
using Nancy.Extensions;
using Shoko.Models.Server;
using NHibernate;
using NHibernate.Util;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Extensions;

namespace Shoko.Server.Repositories.Cached
{
    public class VideoLocalRepository : BaseCachedRepository<SVR_VideoLocal, int>
    {
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
            {
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
                                int b = a.FileName.LastIndexOf("\\", StringComparison.Ordinal);
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
                    int b = a.FileName.LastIndexOf("\\", StringComparison.Ordinal);
                    a.FileName = a.FileName.Substring(b + 1);
                    Save(a, false);
                    count++;
                    ServerState.Instance.CurrentSetupStatus = string.Format(
                        Commons.Properties.Resources.Database_Cache, typeof(VideoLocal).Name,
                        " Cleaning File Paths - " + count + "/" + max);
                });
            }
            catch
            {
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
                    {
                        DeleteWithOpenTransaction(session, remove);
                    }
                    transaction.Commit();
                }

                ServerState.Instance.CurrentSetupStatus = string.Format(
                    Commons.Properties.Resources.Database_Cache, typeof(VideoLocal).Name, " Cleaning Fragmented Records");
                using (var transaction = session.BeginTransaction())
                {
                    var list = Cache.Values.SelectMany(a => RepoFactory.CrossRef_File_Episode.GetByHash(a.Hash))
                        .Where(a => RepoFactory.AniDB_Anime.GetByID(a.AnimeID) == null ||
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

        //Disable base saves.
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Delete(IReadOnlyCollection<SVR_VideoLocal> objs)
        {
            throw new NotSupportedException();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Delete(int id)
        {
            throw new NotSupportedException();
        }

        //Disable base saves.
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Save(SVR_VideoLocal obj)
        {
            throw new NotSupportedException();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Save(IReadOnlyCollection<SVR_VideoLocal> objs)
        {
            throw new NotSupportedException();
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
            {
                foreach (SVR_AnimeEpisode ep in obj.GetAnimeEpisodes())
                {
                    RepoFactory.AnimeEpisode.Save(ep);
                }
            }
        }


        public SVR_VideoLocal GetByHash(string hash)
        {
            return Hashes.GetOne(hash);
        }

        public SVR_VideoLocal GetByMD5(string hash)
        {
            return MD5.GetOne(hash);
        }

        public SVR_VideoLocal GetBySHA1(string hash)
        {
            return SHA1.GetOne(hash);
        }

        public long GetTotalRecordCount()
        {
            return Cache.Keys.Count;
        }

        public SVR_VideoLocal GetByHashAndSize(string hash, long fsize)
        {
            return Hashes.GetMultiple(hash).FirstOrDefault(a => a.FileSize == fsize);
        }

        public List<SVR_VideoLocal> GetByName(string fileName)
        {
            //return Paths.GetMultiple(fileName);
            //return Cache.Values.Where(store => store.FilePath.Contains(fileName, StringComparison.InvariantCultureIgnoreCase)).ToList();

            return Cache.Values.Where(p => p.Places.Any(
                    a => a.FilePath.Contains(fileName, StringComparison.InvariantCultureIgnoreCase)))
                .ToList();
        }

        public List<SVR_VideoLocal> GetMostRecentlyAdded(int maxResults)
        {
            if (maxResults == -1)
                return Cache.Values.OrderByDescending(a => a.DateTimeCreated).ToList();
            return Cache.Values.OrderByDescending(a => a.DateTimeCreated).Take(maxResults).ToList();
        }

        public List<SVR_VideoLocal> GetRandomFiles(int maxResults)
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

        public class UniqueRandoms : IEnumerable<int>
        {
            Random _rand = new Random();
            List<int> _candidates;

            public UniqueRandoms(int maxInclusive)
                : this(1, maxInclusive)
            {
            }

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
        /// <param name="animeEpisodeID"></param>
        /// <returns></returns>
        /// 
        public List<SVR_VideoLocal> GetByAniDBEpisodeID(int episodeID)
        {
            return RepoFactory.CrossRef_File_Episode.GetByEpisodeID(episodeID)
                .Select(a => GetByHash(a.Hash))
                .Where(a => a != null)
                .ToList();
            /*
            return
                session.CreateQuery(
                    "Select vl.VideoLocalID FROM VideoLocal as vl, CrossRef_File_Episode as xref WHERE vl.Hash = xref.Hash AND xref.EpisodeID= :episodeid")
                    .SetParameter("episodeid", episodeID)
                    .List<int>().Select(a => Cache.Get(a)).Where(a => a != null).ToList();*/
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
            /*
                        return
                            session.CreateQuery(
                                "Select vl.VideoLocalID FROM VideoLocal as vl, CrossRef_File_Episode as xref WHERE vl.Hash = xref.Hash AND xref.AnimeID= :animeid ORDER BY vl.DateTimeCreated Desc")
                                .SetParameter("animeid", animeID)
                                .SetMaxResults(maxResults)
                                .List<int>().Select(a => Cache.Get(a)).Where(a => a != null).ToList();*/
        }


        public List<SVR_VideoLocal> GetByAniDBResolution(string res)
        {
            return RepoFactory.AniDB_File.GetByResolution(res)
                .Select(a => GetByHash(a.Hash))
                .Where(a => a != null)
                .ToList();
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return
                    session.CreateQuery(
                        "Select vl.VideoLocalID FROM VideoLocal as vl, AniDB_File as xref WHERE vl.Hash = xref.Hash AND vl.FileSize = xref.FileSize AND xref.File_VideoResolution= :fileres")
                        .SetParameter("fileres", res)
                        .List<int>().Select(a => Cache.Get(a)).Where(a => a != null).ToList();
            }*/
        }

        public List<SVR_VideoLocal> GetByInternalVersion(int iver)
        {
            return RepoFactory.AniDB_File.GetByInternalVersion(iver)
                .Select(a => GetByHash(a.Hash))
                .Where(a => a != null)
                .ToList();
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return
                    session.CreateQuery(
                        "Select vl.VideoLocalID FROM VideoLocal as vl, AniDB_File as xref WHERE vl.Hash = xref.Hash AND vl.FileSize = xref.FileSize AND xref.InternalVersion= :iver")
                        .SetParameter("iver", iver)
                        .List<int>().Select(a => Cache.Get(a)).Where(a => a != null).ToList();
            }*/
        }

        public List<SVR_VideoLocal> GetWithMissingChapters()
        {
            return RepoFactory.AniDB_File.GetWithWithMissingChapters()
                .Select(a => GetByHash(a.Hash))
                .Where(a => a != null)
                .ToList();
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return
                    session.CreateQuery(
                        "Select vl.VideoLocalID FROM VideoLocal as vl, AniDB_File as xref WHERE vl.Hash = xref.Hash AND vl.FileSize = xref.FileSize AND xref.InternalVersion= :iver")
                        .SetParameter("iver", iver)
                        .List<int>().Select(a => Cache.Get(a)).Where(a => a != null).ToList();
            }*/
        }

        /// <summary>
        /// returns all the VideoLocal records associate with an AniDB_Anime Record
        /// </summary>
        /// <param name="animeEpisodeID"></param>
        /// <returns></returns>
        public List<SVR_VideoLocal> GetByAniDBAnimeID(int animeID)
        {
            return
                RepoFactory.CrossRef_File_Episode.GetByAnimeID(animeID)
                    .Select(a => GetByHash(a.Hash))
                    .Where(a => a != null)
                    .ToList();
            /*
            return
                session.CreateQuery(
                    "Select vl.VideoLocalID FROM VideoLocal as vl, CrossRef_File_Episode as xref WHERE vl.Hash = xref.Hash AND xref.AnimeID= :animeID")
                    .SetParameter("animeID", animeID)
                    .List<int>().Select(a => Cache.Get(a)).Where(a => a != null).ToList();*/
        }


        /*
        public List<VideoLocal> GetVideosWithoutImportFolder()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return
                    session.CreateQuery(
                        "Select vl.VideoLocalID FROM VideoLocal vl WHERE vl.ImportFolderID NOT IN (select ImportFolderID from ImportFolder fldr)")
                        .List<int>().Select(a => Cache.Get(a)).Where(a => a != null).ToList();
            }
        }
        */
        public List<SVR_VideoLocal> GetVideosWithoutHash()
        {
            return Hashes.GetMultiple("").ToList();
        }

        public List<SVR_VideoLocal> GetVideosWithoutVideoInfo()
        {
            return Cache.Values.Where(a => a.Media == null || a.MediaVersion < SVR_VideoLocal.MEDIA_VERSION ||
                                           a.Duration == 0)
                .ToList();
        }

        public List<SVR_VideoLocal> GetVideosWithoutEpisode()
        {
            HashSet<string> hashes = new HashSet<string>(RepoFactory.CrossRef_File_Episode.GetAll()
                .Select(a => a.Hash));
            HashSet<string> vlocals = new HashSet<string>(Cache.Values.Where(a => a.IsIgnored == 0)
                .Select(a => a.Hash));
            return vlocals.Except(hashes)
                .SelectMany(a => Hashes.GetMultiple(a))
                .OrderBy(a => a.DateTimeCreated)
                .ToList();

            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return
                    session.CreateQuery(
                        "Select vl.VideoLocalID FROM VideoLocal vl WHERE vl.Hash NOT IN (Select Hash FROM CrossRef_File_Episode xref) AND vl.IsIgnored = 0")
                        .List<int>()
                        .Select(a => Cache.Get(a))
                        .Where(a => a != null)
                        .OrderBy(a => a.DateTimeCreated)
                        .ToList();
            }*/
        }

        // This is impossible with current db, as there is no record of what used added file and VideoLocalUser store only usere related data about known eps
        //public List<VideoLocal> GetVideosWithoutEpisode(int user_id)
        //{
        // get all vl_id for user
        // HashSet<int> user_files = new HashSet<int>(RepoFactory.VideoLocalUser.GetAll().Where(a => a.JMMUserID == user_id).Select(a => a.VideoLocalID));
        // get all hashes for user knowing its vl_id
        // HashSet<string> vlocals = new HashSet<string>(Cache.Values.Where(a => user_files.Contains(a.VideoLocalID)).Where(a => a.IsIgnored == 0).Select(a => a.Hash));
        // get all recognized episode hashes
        // HashSet<string> hashes = new HashSet<string>(RepoFactory.CrossRef_File_Episode.GetAll().Select(a => a.Hash));
        // substract all recognized episode hashes from all hashes for user
        // return vlocals.Except(hashes).SelectMany(a => Hashes.GetMultiple(a)).OrderBy(a => a.DateTimeCreated).ToList();
        // HashSet<string> ret = new HashSet<string>();
        //foreach (string x in vlocals)
        //{
        //    if (!hashes.Contains(x)) { ret.Add(x); }
        //}
        //return ret.SelectMany(a => Hashes.GetMultiple(a)).OrderBy(a => a.DateTimeCreated).ToList();
        //}

        public List<SVR_VideoLocal> GetManuallyLinkedVideos()
        {
            return
                RepoFactory.CrossRef_File_Episode.GetAll()
                    .Where(a => a.CrossRefSource != 1)
                    .Select(a => GetByHash(a.Hash))
                    .Where(a => a != null)
                    .ToList();

            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return
                    session.CreateQuery(
                        "Select vl.VideoLocalID FROM VideoLocal vl WHERE vl.Hash IN (Select Hash FROM CrossRef_File_Episode xref WHERE xref.CrossRefSource <> 1)")
                        .List<int>()
                        .Select(a => Cache.Get(a))
                        .Where(a => a != null)
                        .OrderBy(a => a.DateTimeCreated)
                        .ToList();
            }*/
        }

        public List<SVR_VideoLocal> GetIgnoredVideos()
        {
            return Ignored.GetMultiple(1);
        }
    }
}