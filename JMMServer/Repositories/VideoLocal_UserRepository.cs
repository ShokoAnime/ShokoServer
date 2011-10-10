using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;


namespace JMMServer.Repositories
{
	public class VideoLocal_UserRepository
	{
		public void Save(VideoLocal_User obj)
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

		public VideoLocal_User GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<VideoLocal_User>(id);
			}
		}

		public List<VideoLocal_User> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(VideoLocal_User))
					.List<VideoLocal_User>();

				return new List<VideoLocal_User>(objs);
			}
		}

		public List<VideoLocal_User> GetByVideoLocalID(int vidid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var eps = session
					.CreateCriteria(typeof(VideoLocal_User))
					.Add(Restrictions.Eq("VideoLocalID", vidid))
					.List<VideoLocal_User>();

				return new List<VideoLocal_User>(eps);
			}
		}

		public List<VideoLocal_User> GetByUserID(int userid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var vids = session
					.CreateCriteria(typeof(VideoLocal_User))
					.Add(Restrictions.Eq("JMMUserID", userid))
					.List<VideoLocal_User>();

				return new List<VideoLocal_User>(vids);
			}
		}

		public VideoLocal_User GetByUserIDAndVideoLocalID(int userid, int vidid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				VideoLocal_User obj = session
					.CreateCriteria(typeof(VideoLocal_User))
					.Add(Restrictions.Eq("JMMUserID", userid))
					.Add(Restrictions.Eq("VideoLocalID", vidid))
					.UniqueResult<VideoLocal_User>();

				return obj;
			}
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
						session.Delete(cr);
						transaction.Commit();
					}
				}
			}
		}
	}
}
