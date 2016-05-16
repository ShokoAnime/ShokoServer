using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
using JMMServer.Databases;
using JMMServer.Entities;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;


namespace JMMServer.Repositories
{
	public class VideoLocal_UserRepository
	{
	    private static PocoCache<int, VideoLocal_User> Cache;
	    private static PocoIndex<int, VideoLocal_User, int> VideoLocalIDs;
	    private static PocoIndex<int, VideoLocal_User, int> Users;
	    private static PocoIndex<int, VideoLocal_User, int, int> UsersVideoLocals;

        public static void InitCache()
        {
            string t = "VideoLocal_Users";
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, string.Empty);
            VideoLocal_UserRepository repo = new VideoLocal_UserRepository();
            Cache = new PocoCache<int, VideoLocal_User>(repo.InternalGetAll(), a => a.VideoLocal_UserID);
            VideoLocalIDs=new PocoIndex<int, VideoLocal_User, int>(Cache,a=>a.VideoLocalID);
            Users=new PocoIndex<int, VideoLocal_User, int>(Cache,a=>a.JMMUserID);
            UsersVideoLocals=new PocoIndex<int, VideoLocal_User, int, int>(Cache,a=>a.JMMUserID,a=>a.VideoLocalID);
        }

        private List<VideoLocal_User> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(VideoLocal_User))
                    .List<VideoLocal_User>();

                return new List<VideoLocal_User>(objs);
            }
        }
        public void Save(VideoLocal_User obj)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
                Cache.Update(obj);
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					session.SaveOrUpdate(obj);
					transaction.Commit();
				}
			}
		}

		public VideoLocal_User GetByID(int id)
		{
		    return Cache.Get(id);
		}

		public List<VideoLocal_User> GetAll()
		{
		    return Cache.Values.ToList();
		}

		public List<VideoLocal_User> GetByVideoLocalID(int vidid)
		{
		    return VideoLocalIDs.GetMultiple(vidid);
		}

		public List<VideoLocal_User> GetByUserID(int userid)
		{
		    return Users.GetMultiple(userid);
		}

		public VideoLocal_User GetByUserIDAndVideoLocalID(int userid, int vidid)
		{
		    return UsersVideoLocals.GetOne(userid, vidid);
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					VideoLocal_User cr = GetByID(id);
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
