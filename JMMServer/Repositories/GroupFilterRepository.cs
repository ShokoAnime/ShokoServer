using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;
using NLog;

namespace JMMServer.Repositories
{
	public class GroupFilterRepository
	{
        private static Logger logger = LogManager.GetCurrentClassLogger();

		public void Save(GroupFilter obj)
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
            logger.Trace("Updating group filter stats by groupfilter from GroupFilterRepository.Save: {0}", obj.GroupFilterID);
            StatsCache.Instance.UpdateGroupFilterUsingGroupFilter(obj.GroupFilterID);
		}

		public GroupFilter GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<GroupFilter>(id);
			}
		}

		public List<GroupFilter> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var gfs = session
					.CreateCriteria(typeof(GroupFilter))
					.List<GroupFilter>();
				return new List<GroupFilter>(gfs);
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					GroupFilter cr = GetByID(id);
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
