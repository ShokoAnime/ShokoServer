﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Databases;
using JMMServer.Entities;
using NHibernate.Criterion;
using NHibernate;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
	public class VideoLocalRepository
	{
	    private static PocoCache<int, VideoLocal> Cache;
	    private static PocoIndex<int, VideoLocal, string> Hashes;
	    private static PocoIndex<int, VideoLocal, string> Paths;
	    private static PocoIndex<int, VideoLocal, int> Ignored;
	    private static PocoIndex<int, VideoLocal, int> ImportFolders;
        public static void InitCache()
        {
            string t = "VideoLocal";
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, string.Empty);
            VideoLocalRepository repo = new VideoLocalRepository();
            Cache = new PocoCache<int, VideoLocal>(repo.InternalGetAll(), a => a.VideoLocalID);
            Hashes = new PocoIndex<int, VideoLocal, string>(Cache,a=>a.Hash);
            Paths=new PocoIndex<int, VideoLocal, string>(Cache,a=>a.FilePath);
            Ignored=new PocoIndex<int, VideoLocal, int>(Cache,a=>a.IsIgnored);
            ImportFolders=new PocoIndex<int, VideoLocal, int>(Cache,a=>a.ImportFolderID);

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

        public void Save(VideoLocal obj)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					session.SaveOrUpdate(obj);
					transaction.Commit();
				}
                Cache.Update(obj);
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
		    return Paths.GetMultiple(fileName);
		}

		public List<VideoLocal> GetMostRecentlyAdded(int maxResults)
		{
		    return Cache.Values.OrderByDescending(a => a.DateTimeCreated).Take(15).ToList();
		}

		public List<VideoLocal> GetMostRecentlyAdded(ISession session, int maxResults)
		{
            return GetMostRecentlyAdded(maxResults);
		}

		public List<VideoLocal> GetRandomFiles(int maxResults)
		{

            IEnumerator<int> en= new UniqueRandoms(0, Cache.Values.Count - 1).GetEnumerator();
            List<VideoLocal> vids=new List<VideoLocal>();
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
            { }

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
        public List<VideoLocal> GetByFilePathAndShareID(string filePath, int nshareID)
        {
            return Paths.GetMultiple(filePath).Where(a => a.ImportFolderID == nshareID).ToList();
		}

		/// <summary>
		/// returns all the VideoLocal records associate with an AnimeEpisode Record
		/// </summary>
		/// <param name="animeEpisodeID"></param>
		/// <returns></returns>
		/// 
		public List<VideoLocal> GetByAniDBEpisodeID(int episodeID)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetByAniDBEpisodeID(session, episodeID);
			}
		}

		public List<VideoLocal> GetByAniDBEpisodeID(ISession session, int episodeID)
		{
			return session.CreateQuery("Select vl.VideoLocalID FROM VideoLocal as vl, CrossRef_File_Episode as xref WHERE vl.Hash = xref.Hash AND xref.EpisodeID= :episodeid")
				.SetParameter("episodeid", episodeID)
				.List<int>().Select(a=>Cache.Get(a)).Where(a=>a!=null).ToList();
		}

		public List<VideoLocal> GetMostRecentlyAddedForAnime(int maxResults, int animeID)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetMostRecentlyAddedForAnime(session, maxResults, animeID);
			}
		}

		public List<VideoLocal> GetMostRecentlyAddedForAnime(ISession session, int maxResults, int animeID)
		{
			return session.CreateQuery("Select vl.VideoLocalID FROM VideoLocal as vl, CrossRef_File_Episode as xref WHERE vl.Hash = xref.Hash AND xref.AnimeID= :animeid ORDER BY vl.DateTimeCreated Desc")
				.SetParameter("animeid", animeID)
				.SetMaxResults(maxResults)
				.List<int>().Select(a => Cache.Get(a)).Where(a => a != null).ToList();
		}

		public List<VideoLocal> GetByAniDBResolution(string res)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{

				return session.CreateQuery("Select vl.VideoLocalID FROM VideoLocal as vl, AniDB_File as xref WHERE vl.Hash = xref.Hash AND vl.FileSize = xref.FileSize AND xref.File_VideoResolution= :fileres")
					.SetParameter("fileres", res)
                    .List<int>().Select(a => Cache.Get(a)).Where(a => a != null).ToList();
			}
		}

		public List<VideoLocal> GetByInternalVersion(int iver)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{

				return session.CreateQuery("Select vl.VideoLocalID FROM VideoLocal as vl, AniDB_File as xref WHERE vl.Hash = xref.Hash AND vl.FileSize = xref.FileSize AND xref.InternalVersion= :iver")
					.SetParameter("iver", iver)
                    .List<int>().Select(a => Cache.Get(a)).Where(a => a != null).ToList();
			}
		}

		/// <summary>
		/// returns all the VideoLocal records associate with an AniDB_Anime Record
		/// </summary>
		/// <param name="animeEpisodeID"></param>
		/// <returns></returns>
		public List<VideoLocal> GetByAniDBAnimeID(int animeID)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetByAniDBAnimeID(session, animeID);
			}
		}

		public List<VideoLocal> GetByAniDBAnimeID(ISession session, int animeID)
		{
			return session.CreateQuery("Select vl.VideoLocalID FROM VideoLocal as vl, CrossRef_File_Episode as xref WHERE vl.Hash = xref.Hash AND xref.AnimeID= :animeID")
				.SetParameter("animeID", animeID)
                .List<int>().Select(a => Cache.Get(a)).Where(a => a != null).ToList();
		}

		public List<VideoLocal> GetVideosWithoutImportFolder()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.CreateQuery("FROM VideoLocal vl.VideoLocalID WHERE vl.ImportFolderID NOT IN (select ImportFolderID from ImportFolder fldr)")
                    .List<int>().Select(a => Cache.Get(a)).Where(a => a != null).ToList();
			}
		}

		public List<VideoLocal> GetVideosWithoutHash()
		{
		    return Hashes.GetMultiple("").ToList();
		}

		public List<VideoLocal> GetVideosWithoutVideoInfo()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.CreateQuery("Select vl.VideoLocalID FROM VideoLocal vl WHERE vl.Hash NOT IN (Select Hash FROM VideoInfo vi)")
                    .List<int>().Select(a => Cache.Get(a)).Where(a => a != null).ToList();
			}
		}

		public List<VideoLocal> GetVideosWithoutEpisode()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.CreateQuery("Select vl.VideoLocalID FROM VideoLocal vl WHERE vl.Hash NOT IN (Select Hash FROM CrossRef_File_Episode xref) AND vl.IsIgnored = 0")
                    .List<int>().Select(a => Cache.Get(a)).Where(a => a != null).ToList();
			}
		}


		public List<VideoLocal> GetManuallyLinkedVideos()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.CreateQuery("Select vl.VideoLocalID FROM VideoLocal vl WHERE vl.Hash IN (Select Hash FROM CrossRef_File_Episode xref WHERE xref.CrossRefSource <> 1)")
                    .List<int>().Select(a => Cache.Get(a)).Where(a => a != null).ToList();
			}
		}

		public List<VideoLocal> GetIgnoredVideos()
		{
		    return Ignored.GetMultiple(1);
		}

		public List<VideoLocal> GetByImportFolder(int importFolderID)
		{
		    return ImportFolders.GetMultiple(importFolderID);
		}

		public List<VideoLocal> GetAll()
		{
		    return Cache.Values.ToList();
		}
		

		public void Delete(int id)
		{
			VideoLocal cr = GetByID(id);
			if (cr != null)
			{
                Cache.Remove(cr);
                // delete video info record
                VideoInfoRepository repVI = new VideoInfoRepository();
				VideoInfo vi = cr.VideoInfo;
			    if (vi != null)
			    {
			        repVI.Delete(vi.VideoInfoID);
			    }
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
