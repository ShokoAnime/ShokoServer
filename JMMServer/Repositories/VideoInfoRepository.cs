using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Databases;
using JMMServer.Entities;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
	public class VideoInfoRepository
	{
	    private static PocoCache<int, VideoInfo> Cache;
	    private static PocoIndex<int, VideoInfo, string> Hashes;

        public static void InitCache()
        {
            string t = "VideoInfos";
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, string.Empty);
            VideoInfoRepository repo = new VideoInfoRepository();
            Cache = new PocoCache<int, VideoInfo>(repo.InternalGetAll(), a => a.VideoInfoID);
            Hashes=new PocoIndex<int, VideoInfo, string>(Cache,a=>a.Hash);
        }


        private List<VideoInfo> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(VideoInfo))
                    .List<VideoInfo>();

                return new List<VideoInfo>(objs);
            }
        }

        public void Save(VideoInfo obj)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
                Cache.Update(obj);
				using (var transaction = session.BeginTransaction())
				{
					session.SaveOrUpdate(obj);
					transaction.Commit();
				}
			}
		}

		public VideoInfo GetByID(int id)
		{
		    return Cache.Get(id);
		}

		public VideoInfo GetByHash(string hash)
		{
		    return Hashes.GetOne(hash);
		}


		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					VideoInfo cr = GetByID(id);
					if (cr != null)
					{
                        Cache.Remove(cr);
						session.Delete(cr);
						transaction.Commit();
					}
				}
			}
		}
	}
}
