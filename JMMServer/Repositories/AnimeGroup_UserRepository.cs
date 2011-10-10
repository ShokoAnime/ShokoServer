using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;
using NLog;

namespace JMMServer.Repositories
{
	public class AnimeGroup_UserRepository
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public void Save(AnimeGroup_User obj)
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
			//logger.Trace("Updating group stats by group from AnimeGroup_UserRepository.Save: {0}", obj.AnimeGroupID);
			//StatsCache.Instance.UpdateUsingGroup(obj.AnimeGroupID);
		}

		public AnimeGroup_User GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<AnimeGroup_User>(id);
			}
		}

		public AnimeGroup_User GetByUserAndGroupID(int userid, int groupid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				AnimeGroup_User cr = session
					.CreateCriteria(typeof(AnimeGroup_User))
					.Add(Restrictions.Eq("JMMUserID", userid))
					.Add(Restrictions.Eq("AnimeGroupID", groupid))
					.UniqueResult<AnimeGroup_User>();
				return cr;
			}
		}

		public List<AnimeGroup_User> GetByUserID(int userid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var grps = session
					.CreateCriteria(typeof(AnimeGroup_User))
					.Add(Restrictions.Eq("JMMUserID", userid))
					.List<AnimeGroup_User>();

				return new List<AnimeGroup_User>(grps);
			}
		}

		public List<AnimeGroup_User> GetByGroupID(int groupid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var grps = session
					.CreateCriteria(typeof(AnimeGroup_User))
					.Add(Restrictions.Eq("AnimeGroupID", groupid))
					.List<AnimeGroup_User>();

				return new List<AnimeGroup_User>(grps);
			}
		}

		public List<AnimeGroup_User> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var grps = session
					.CreateCriteria(typeof(AnimeGroup_User))
					.List<AnimeGroup_User>();

				return new List<AnimeGroup_User>(grps);
			}
		}

		public void Delete(int id)
		{
			AnimeGroup_User cr = null;
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					cr = GetByID(id);
					if (cr != null)
					{
						session.Delete(cr);
						transaction.Commit();
					}
				}
			}

			//if (cr != null)
			//{
			//	logger.Trace("Updating group stats by group from AnimeGroupRepository.Delete: {0}", cr.AnimeGroupID);
			//	StatsCache.Instance.UpdateUsingGroup(cr.AnimeGroupID);
			//}
		}
	}
}
