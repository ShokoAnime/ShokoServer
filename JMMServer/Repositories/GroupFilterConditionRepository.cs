using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;
using NLog;
using NHibernate;

namespace JMMServer.Repositories
{
	public class GroupFilterConditionRepository
	{
        private static Logger logger = LogManager.GetCurrentClassLogger();


		public void Save(GroupFilterCondition obj)
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

            logger.Trace("Updating group filter stats by groupfilter condition from GroupFilterConditionRepository.Save: {0}", obj.GroupFilterID);
            StatsCache.Instance.UpdateGroupFilterUsingGroupFilter(obj.GroupFilterID);
		}

		public GroupFilterCondition GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<GroupFilterCondition>(id);
			}
		}

		public List<GroupFilterCondition> GetByGroupFilterID(int gfid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetByGroupFilterID(session, gfid);
			}
		}

		public List<GroupFilterCondition> GetByGroupFilterID(ISession session, int gfid)
		{
			var gfcs = session
				.CreateCriteria(typeof(GroupFilterCondition))
				.Add(Restrictions.Eq("GroupFilterID", gfid))
				.List<GroupFilterCondition>();

			return new List<GroupFilterCondition>(gfcs);
		}

		public List<GroupFilterCondition> GetByConditionType(GroupFilterConditionType ctype)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var gfcs = session
					.CreateCriteria(typeof(GroupFilterCondition))
					.Add(Restrictions.Eq("ConditionType", (int)ctype))
					.List<GroupFilterCondition>();

				return new List<GroupFilterCondition>(gfcs);
			}
		}

		public List<GroupFilterCondition> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var gfcs = session
					.CreateCriteria(typeof(GroupFilterCondition))
					.List<GroupFilterCondition>();
				return new List<GroupFilterCondition>(gfcs);
			}
		}

		public void Delete(int id)
		{
		    GroupFilterCondition cr= null;
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
            if (cr!=null)
            {
                logger.Trace("Updating group filter stats by groupfilter condition from GroupFilterConditionRepository.Delete: {0}", cr.GroupFilterID);
                StatsCache.Instance.UpdateGroupFilterUsingGroupFilter(cr.GroupFilterID);
            }
		}
	}
}
