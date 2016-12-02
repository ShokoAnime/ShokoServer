using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using FluentNHibernate.Utils;
using JMMServer.Entities;
using NHibernate;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories.Cached
{
    public class VideoLocalRepository : BaseCachedRepository<VideoLocal,int>
    {
        private PocoIndex<int, VideoLocal, string> Hashes;
        private PocoIndex<int, VideoLocal, string> SHA1;
        private PocoIndex<int, VideoLocal, string> MD5;
        private PocoIndex<int, VideoLocal, int> Ignored;

        private VideoLocalRepository()
        {
            DeleteWithOpenTransactionCallback = (ses, obj) =>
            {
                RepoFactory.VideoLocalPlace.DeleteWithOpenTransaction(ses, obj.Places.ToList());
                RepoFactory.VideoLocalUser.DeleteWithOpenTransaction(ses, RepoFactory.VideoLocalUser.GetByVideoLocalID(obj.VideoLocalID));
            };
        }

        public static VideoLocalRepository Create()
        {
            return new VideoLocalRepository();
        }

        protected override int SelectKey(VideoLocal entity)
        {
            return entity.VideoLocalID;
        }

        public override void PopulateIndexes()
        {
            //Fix null hashes
            foreach (VideoLocal l in Cache.Values)
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
            Hashes = new PocoIndex<int, VideoLocal, string>(Cache, a => a.Hash);
            SHA1 = new PocoIndex<int, VideoLocal, string>(Cache, a => a.SHA1);
            MD5 = new PocoIndex<int, VideoLocal, string>(Cache, a => a.MD5);
            Ignored = new PocoIndex<int, VideoLocal, int>(Cache, a => a.IsIgnored);
        }

        public override void RegenerateDb()
        {
            RegenerateDb(Cache.Values.Where(a => a.MediaVersion < VideoLocal.MEDIA_VERSION || a.MediaBlob == null).ToList(),a=>
            {
                //Fix possible paths in filename
                if (!string.IsNullOrEmpty(a.FileName))
                {
                    int b = a.FileName.LastIndexOf("\\");
                    if (b>0)
                        a.FileName = a.FileName.Substring(b + 1);
                }
                Save(a, false);
            });
            //Fix possible paths in filename
            Cache.Values.Where(a=>a.FileName.Contains("\\")).ToList().ForEach(a =>
            {
                int b = a.FileName.LastIndexOf("\\");
                a.FileName = a.FileName.Substring(b + 1);
                Save(a,false);
            });
        }


        public List<VideoLocal> GetByImportFolder(int importFolderID)
        {
            return RepoFactory.VideoLocalPlace.GetByImportFolder(importFolderID).Select(a=>a.VideoLocal).Where(a=>a!=null).Distinct().ToList();
        }

        private void UpdateMediaContracts(VideoLocal obj)
        {
            if (obj.Media == null || obj.MediaVersion < VideoLocal.MEDIA_VERSION || obj.Duration==0)
            {
                VideoLocal_Place place = obj.GetBestVideoLocalPlace();
                place?.RefreshMediaInfo();
            }
        }

        public override void Delete(VideoLocal obj)
        {
            base.Delete(obj);
            foreach (AnimeEpisode ep in obj.GetAnimeEpisodes())
            {
                RepoFactory.AnimeEpisode.Save(ep);
            }
        }
        //Disable base saves.
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Delete(IReadOnlyCollection<VideoLocal> objs) { throw new NotSupportedException(); }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Delete(int id) { throw new NotSupportedException(); }

        //Disable base saves.
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Save(VideoLocal obj) { throw new NotSupportedException(); }
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Save(IReadOnlyCollection<VideoLocal> objs) { throw new NotSupportedException(); }


        public void Save(VideoLocal obj, bool updateEpisodes)
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
                foreach (AnimeEpisode ep in obj.GetAnimeEpisodes())
                {
                    RepoFactory.AnimeEpisode.Save(ep);
                }
            }
        }

        
        public VideoLocal GetByHash(string hash)
        {
            return Hashes.GetOne(hash);
        }

        public VideoLocal GetByMD5(string hash)
        {
            return MD5.GetOne(hash);
        }
        public VideoLocal GetBySHA1(string hash)
        {
            return SHA1.GetOne(hash);
        }
        public long GetTotalRecordCount()
        {
            return Cache.Keys.Count;
        }

        public VideoLocal GetByHashAndSize(string hash, long fsize)
        {
            return Hashes.GetMultiple(hash).FirstOrDefault(a => a.FileSize == fsize);
        }

        public List<VideoLocal> GetByName(string fileName)
        {
            //return Paths.GetMultiple(fileName);
            //return Cache.Values.Where(store => store.FilePath.Contains(fileName, StringComparison.InvariantCultureIgnoreCase)).ToList();

            return Cache.Values.Where(p => p.Places.Any(a=>CultureInfo.CurrentCulture.CompareInfo.IndexOf(a.FilePath, fileName, CompareOptions.IgnoreCase) >= 0)).ToList();
        }

        public List<VideoLocal> GetMostRecentlyAdded(int maxResults)
        {
            return Cache.Values.OrderByDescending(a => a.DateTimeCreated).Take(maxResults).ToList();
        }

        public List<VideoLocal> GetRandomFiles(int maxResults)
        {
            IEnumerator<int> en = new UniqueRandoms(0, Cache.Values.Count - 1).GetEnumerator();
            List<VideoLocal> vids = new List<VideoLocal>();
            if (maxResults > Cache.Values.Count)
                maxResults = Cache.Values.Count;
            for (int x = 0; x < maxResults; x++)
            {
                en.MoveNext();
                vids.Add(Cache.Values.ElementAt(en.Current));
            }
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
        public List<VideoLocal> GetByAniDBEpisodeID(int episodeID)
        {
            return RepoFactory.CrossRef_File_Episode.GetByEpisodeID(episodeID).Select(a => GetByHash(a.Hash)).Where(a => a != null).ToList();
            /*
            return
                session.CreateQuery(
                    "Select vl.VideoLocalID FROM VideoLocal as vl, CrossRef_File_Episode as xref WHERE vl.Hash = xref.Hash AND xref.EpisodeID= :episodeid")
                    .SetParameter("episodeid", episodeID)
                    .List<int>().Select(a => Cache.Get(a)).Where(a => a != null).ToList();*/
        }



        public List<VideoLocal> GetMostRecentlyAddedForAnime(int maxResults, int animeID)
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


        public List<VideoLocal> GetByAniDBResolution(string res)
        {
            return RepoFactory.AniDB_File.GetByResolution(res).Select(a => GetByHash(a.Hash)).Where(a => a != null).ToList();
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

        public List<VideoLocal> GetByInternalVersion(int iver)
        {
            return RepoFactory.AniDB_File.GetByInternalVersion(iver).Select(a => GetByHash(a.Hash)).Where(a => a != null).ToList();
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
        public List<VideoLocal> GetByAniDBAnimeID(int animeID)
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
        public List<VideoLocal> GetVideosWithoutHash()
        {
            return Hashes.GetMultiple("").ToList();
        }
        public List<VideoLocal> GetVideosWithoutVideoInfo()
        {
            return Cache.Values.Where(a => a.Media == null || a.MediaVersion < VideoLocal.MEDIA_VERSION || a.Duration==0).ToList();
        }
        public List<VideoLocal> GetVideosWithoutEpisode()
        {
            HashSet<string> hashes = new HashSet<string>(RepoFactory.CrossRef_File_Episode.GetAll().Select(a => a.Hash));
            HashSet<string> vlocals=new HashSet<string>(Cache.Values.Where(a=>a.IsIgnored==0).Select(a=>a.Hash));
            return vlocals.Except(hashes).SelectMany(a=>Hashes.GetMultiple(a)).OrderBy(a=>a.DateTimeCreated).ToList();

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

        public List<VideoLocal> GetManuallyLinkedVideos()
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

        public List<VideoLocal> GetIgnoredVideos()
        {
            return Ignored.GetMultiple(1);
        }

    }
}