using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
	public class Trakt_SeasonRepository
	{
		public void Save(Trakt_Season obj)
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

		public Trakt_Season GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<Trakt_Season>(id);
			}
		}

		public List<Trakt_Season> GetByShowID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(Trakt_Season))
					.Add(Restrictions.Eq("Trakt_ShowID", id))
					.List<Trakt_Season>();

				return new List<Trakt_Season>(objs);
			}
		}

		public Trakt_Season GetByShowIDAndSeason(int id, int season)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				Trakt_Season obj = session
					.CreateCriteria(typeof(Trakt_Season))
					.Add(Restrictions.Eq("Trakt_ShowID", id))
					.Add(Restrictions.Eq("Season", season))
					.UniqueResult<Trakt_Season>();

				return obj;
			}
		}

		public List<Trakt_Season> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(Trakt_Season))
					.List<Trakt_Season>();

				return new List<Trakt_Season>(objs);
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					Trakt_Season cr = GetByID(id);
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
