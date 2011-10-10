using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
	public class GroupFilterConditionRepository
	{
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
				var gfcs = session
					.CreateCriteria(typeof(GroupFilterCondition))
					.Add(Restrictions.Eq("GroupFilterID", gfid))
					.List<GroupFilterCondition>();

				return new List<GroupFilterCondition>(gfcs);
			}
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
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					GroupFilterCondition cr = GetByID(id);
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
