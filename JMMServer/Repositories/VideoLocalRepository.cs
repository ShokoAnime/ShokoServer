using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;
using NHibernate;

namespace JMMServer.Repositories
{
	public class VideoLocalRepository
	{
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
			}
		}

		public VideoLocal GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<VideoLocal>(id);
			}
		}


		public VideoLocal GetByHash(string hash)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				VideoLocal obj = session
					.CreateCriteria(typeof(VideoLocal))
					.Add(Restrictions.Eq("Hash", hash))
					.UniqueResult<VideoLocal>();

				return obj;
			}
		}

        public long GetTotalRecordCount()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var count = session
                    .CreateCriteria(typeof(VideoLocal))
                    .SetProjection(Projections.Count(Projections.Id())
                )
                .UniqueResult<int>();

                return count;
            }
        }

		public VideoLocal GetByHashAndSize(string hash, long fsize)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				VideoLocal obj = session
					.CreateCriteria(typeof(VideoLocal))
					.Add(Restrictions.Eq("Hash", hash))
					.Add(Restrictions.Eq("FileSize", fsize))
					.UniqueResult<VideoLocal>();

				return obj;
			}
		}

		public List<VideoLocal> GetByName(string fileName)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var eps = session
					.CreateCriteria(typeof(VideoLocal))
					.Add(Restrictions.Like("FilePath", fileName, MatchMode.Anywhere))
					.List<VideoLocal>();

				return new List<VideoLocal>(eps);
			}
		}

		public List<VideoLocal> GetMostRecentlyAdded(int maxResults)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetMostRecentlyAdded(session, maxResults);
			}
		}

		public List<VideoLocal> GetMostRecentlyAdded(ISession session, int maxResults)
		{
			var eps = session
				.CreateCriteria(typeof(VideoLocal))
				.AddOrder(Order.Desc("DateTimeCreated"))
				.SetMaxResults(maxResults + 15)
				.List<VideoLocal>();

			return new List<VideoLocal>(eps);
		}

		public List<VideoLocal> GetRandomFiles(int maxResults)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var eps = session
					.CreateCriteria(typeof(VideoLocal))
					.SetMaxResults(maxResults)
					.List<VideoLocal>();

				return new List<VideoLocal>(eps);
			}
		}

		public List<VideoLocal> GetByFilePathAndShareID(string filePath, int nshareID)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var vidfiles = session
					.CreateCriteria(typeof(VideoLocal))
					.Add(Restrictions.Eq("FilePath", filePath))
					.Add(Restrictions.Eq("ImportFolderID", nshareID))
					.List<VideoLocal>();
				return new List<VideoLocal>(vidfiles);
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
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetByAniDBEpisodeID(session, episodeID);
			}
		}

		public List<VideoLocal> GetByAniDBEpisodeID(ISession session, int episodeID)
		{
			var vidfiles = session.CreateQuery("Select vl FROM VideoLocal as vl, CrossRef_File_Episode as xref WHERE vl.Hash = xref.Hash AND xref.EpisodeID= :episodeid")
				.SetParameter("episodeid", episodeID)
				.List<VideoLocal>();

			return new List<VideoLocal>(vidfiles);
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
			var vidfiles = session.CreateQuery("Select vl FROM VideoLocal as vl, CrossRef_File_Episode as xref WHERE vl.Hash = xref.Hash AND xref.AnimeID= :animeid ORDER BY vl.DateTimeCreated Desc")
				.SetParameter("animeid", animeID)
				.SetMaxResults(maxResults)
				.List<VideoLocal>();

			return new List<VideoLocal>(vidfiles);
		}

		public List<VideoLocal> GetByAniDBResolution(string res)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{

				var vidfiles = session.CreateQuery("Select vl FROM VideoLocal as vl, AniDB_File as xref WHERE vl.Hash = xref.Hash AND vl.FileSize = xref.FileSize AND xref.File_VideoResolution= :fileres")
					.SetParameter("fileres", res)
					.List<VideoLocal>();

				return new List<VideoLocal>(vidfiles);
			}
		}

		public List<VideoLocal> GetByInternalVersion(int iver)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{

				var vidfiles = session.CreateQuery("Select vl FROM VideoLocal as vl, AniDB_File as xref WHERE vl.Hash = xref.Hash AND vl.FileSize = xref.FileSize AND xref.InternalVersion= :iver")
					.SetParameter("iver", iver)
					.List<VideoLocal>();

				return new List<VideoLocal>(vidfiles);
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
			var vidfiles = session.CreateQuery("Select vl FROM VideoLocal as vl, CrossRef_File_Episode as xref WHERE vl.Hash = xref.Hash AND xref.AnimeID= :animeID")
				.SetParameter("animeID", animeID)
				.List<VideoLocal>();

			return new List<VideoLocal>(vidfiles);
		}

		public List<VideoLocal> GetVideosWithoutImportFolder()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var vidfiles = session.CreateQuery("FROM VideoLocal vl WHERE vl.ImportFolderID NOT IN (select ImportFolderID from ImportFolder fldr)")
					.List<VideoLocal>();

				return new List<VideoLocal>(vidfiles);
			}
		}

		public List<VideoLocal> GetVideosWithoutHash()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var vidfiles = session.CreateQuery("FROM VideoLocal vl WHERE vl.Hash = ''")
					.List<VideoLocal>();

				return new List<VideoLocal>(vidfiles);
			}
		}

		public List<VideoLocal> GetVideosWithoutVideoInfo()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var vidfiles = session.CreateQuery("FROM VideoLocal vl WHERE vl.Hash NOT IN (Select Hash FROM VideoInfo vi)")
					.List<VideoLocal>();

				return new List<VideoLocal>(vidfiles);
			}
		}

		public List<VideoLocal> GetVideosWithoutEpisode()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var vidfiles = session.CreateQuery("FROM VideoLocal vl WHERE vl.Hash NOT IN (Select Hash FROM CrossRef_File_Episode xref) AND vl.IsIgnored = 0")
					.List<VideoLocal>();

				return new List<VideoLocal>(vidfiles);
			}
		}

		public List<VideoLocal> GetManuallyLinkedVideos()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var vidfiles = session.CreateQuery("FROM VideoLocal vl WHERE vl.Hash IN (Select Hash FROM CrossRef_File_Episode xref WHERE xref.CrossRefSource <> 1)")
					.List<VideoLocal>();

				return new List<VideoLocal>(vidfiles);
			}
		}

		public List<VideoLocal> GetIgnoredVideos()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var vidfiles = session
					.CreateCriteria(typeof(VideoLocal))
					.Add(Restrictions.Eq("IsIgnored", 1))
					.List<VideoLocal>();
				return new List<VideoLocal>(vidfiles);
			}
		}

		public List<VideoLocal> GetByImportFolder(int importFolderID)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var vidfiles = session
					.CreateCriteria(typeof(VideoLocal))
					.Add(Restrictions.Eq("ImportFolderID", importFolderID))
					.List<VideoLocal>();
				return new List<VideoLocal>(vidfiles);
			}
		}

		public List<VideoLocal> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(VideoLocal))
					.List<VideoLocal>();

				return new List<VideoLocal>(objs);
			}
		}
		

		public void Delete(int id)
		{
			VideoLocal cr = GetByID(id);
			if (cr != null)
			{
				// delete video info record
				VideoInfoRepository repVI = new VideoInfoRepository();
				VideoInfo vi = cr.VideoInfo;
				if (vi != null)
					repVI.Delete(vi.VideoInfoID);

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
