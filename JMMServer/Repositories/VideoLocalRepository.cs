﻿using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.FileHelper;
using JMMServer.PlexAndKodi;
using NHibernate;
using NutzCode.InMemoryIndex;
using System.Globalization;
using JMMServer.Repositories.NHibernate;

namespace JMMServer.Repositories
{
    public class VideoLocalRepository
    {
        private static PocoCache<int, VideoLocal> Cache;
        private static PocoIndex<int, VideoLocal, string> Hashes;
        private static PocoIndex<int, VideoLocal, string> SHA1;
        private static PocoIndex<int, VideoLocal, string> MD5;

        private static PocoIndex<int, VideoLocal, string> Names;
        private static PocoIndex<int, VideoLocal, int> Ignored;

        public static void InitCache()
        {
            string t = "VideoLocal";
            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t, string.Empty);
            VideoLocalRepository repo = new VideoLocalRepository();
            Cache = new PocoCache<int, VideoLocal>(repo.InternalGetAll(), a => a.VideoLocalID);
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
            SHA1 =new PocoIndex<int, VideoLocal, string>(Cache, a=>a.SHA1);
            MD5 = new PocoIndex<int, VideoLocal, string>(Cache, a => a.MD5);
            Names = new PocoIndex<int, VideoLocal, string>(Cache, a => a.FileName);
            Ignored = new PocoIndex<int, VideoLocal, int>(Cache, a => a.IsIgnored);
            int cnt = 0;
            List<VideoLocal> grps = Cache.Values.Where(a => a.MediaVersion < VideoLocal.MEDIA_VERSION || a.MediaBlob==null).ToList();
            int max = grps.Count;
            foreach (VideoLocal g in grps)
            {
                try
                {
                    repo.Save(g, false);
                }
                catch (Exception)
                {
                    // ignored
                }
                cnt++;
                if (cnt%10 == 0)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t,
                        " DbRegen - " + cnt + "/" + max);
                }
            }
            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t,
                " DbRegen - " + max + "/" + max);
        }

        private List<VideoLocal> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(VideoLocal))
                    .List<VideoLocal>();

                return new List<VideoLocal>(objs);
            }
        }

        public List<VideoLocal> GetByImportFolder(int importFolderID)
        {
            // todo check if needed anymore
            // return ImportFolders.GetMultiple(importFolderID);
            return null;
        }

        private void UpdateMediaContracts(VideoLocal obj)
        {
            if (obj.Media == null || obj.MediaVersion < VideoLocal.MEDIA_VERSION || obj.Duration==0)
            {
                VideoLocal_Place place = obj.GetBestVideoLocalPlace();
                place?.RefreshMediaInfo();
            }
        }

        public void Save(VideoLocal obj, bool updateEpisodes)
        {
            lock (obj)
            {
                if (obj.VideoLocalID == 0)
                {
                    obj.Media = null;
                    using (var session = JMMService.SessionFactory.OpenSession())
                    {
                        // populate the database
                        using (var transaction = session.BeginTransaction())
                        {
                            session.SaveOrUpdate(obj);
                            transaction.Commit();
                        }
                    }
                }
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    UpdateMediaContracts(obj);
                    using (var transaction = session.BeginTransaction())
                    {
                        session.SaveOrUpdate(obj);
                        transaction.Commit();
                    }
                    Cache.Update(obj);
                }
            }
            if (updateEpisodes)
            {
                AnimeEpisodeRepository repo = new AnimeEpisodeRepository();
                foreach (AnimeEpisode ep in obj.GetAnimeEpisodes())
                {
                    repo.Save(ep);
                }
            }
        }

        public VideoLocal GetByID(int id)
        {
            return Cache.Get(id);
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
            return new CrossRef_File_EpisodeRepository().GetByEpisodeID(episodeID).Select(a => GetByHash(a.Hash)).Where(a => a != null).ToList();
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
                new CrossRef_File_EpisodeRepository().GetByAnimeID(animeID)
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
            return new AniDB_FileRepository().GetByResolution(res).Select(a => GetByHash(a.Hash)).Where(a => a != null).ToList();
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
            return new AniDB_FileRepository().GetByInternalVersion(iver).Select(a => GetByHash(a.Hash)).Where(a => a != null).ToList();
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
             new CrossRef_File_EpisodeRepository().GetByAnimeID(animeID)
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
            List<string> hashes = new CrossRef_File_EpisodeRepository().GetAll().Select(a => a.Hash).ToList();
            return Cache.Values.Where(a=>!hashes.Contains(a.Hash) && a.IsIgnored==0).OrderBy(a=>a.DateTimeCreated).ToList();
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


        public List<VideoLocal> GetManuallyLinkedVideos()
        {
            return
                new CrossRef_File_EpisodeRepository().GetAll()
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


        public List<VideoLocal> GetAll()
        {
            return Cache.Values.ToList();
        }


        public void Delete(int id)
        {
            
            VideoLocal cr = GetByID(id);
            lock (cr)
            {
                if (cr != null)
                {
                    Cache.Remove(cr);
                    // delete places
                    VideoLocal_PlaceRepository repPlaces = new VideoLocal_PlaceRepository();
                    foreach (VideoLocal_Place vidplace in cr.Places.ToList())
                        repPlaces.Delete(vidplace.VideoLocal_Place_ID);

                    // delete user records
                    VideoLocal_UserRepository repUsers = new VideoLocal_UserRepository();
                    foreach (VideoLocal_User viduser in repUsers.GetByVideoLocalID(id))
                        repUsers.Delete(viduser.VideoLocal_UserID);
                }

                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    // populate the database
                    using (var transaction = session.BeginTransaction())
                    {
                        if (cr != null)
                        {
                            session.Delete(cr);
                            transaction.Commit();
                        }
                    }
                }
            }
        }
    }
}