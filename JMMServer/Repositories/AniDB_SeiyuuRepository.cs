using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;
using NHibernate;

namespace JMMServer.Repositories
{
	public class AniDB_SeiyuuRepository
	{
		public void Save(AniDB_Seiyuu obj)
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

		public AniDB_Seiyuu GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<AniDB_Seiyuu>(id);
			}
		}

		public List<AniDB_Seiyuu> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(AniDB_Seiyuu))
					.List<AniDB_Seiyuu>();

				return new List<AniDB_Seiyuu>(objs);
			}
		}

		public AniDB_Seiyuu GetBySeiyuuID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				AniDB_Seiyuu cr = session
					.CreateCriteria(typeof(AniDB_Seiyuu))
					.Add(Restrictions.Eq("SeiyuuID", id))
					.UniqueResult<AniDB_Seiyuu>();
				return cr;
			}
		}

		public AniDB_Seiyuu GetBySeiyuuID(ISession session, int id)
		{
			AniDB_Seiyuu cr = session
				.CreateCriteria(typeof(AniDB_Seiyuu))
				.Add(Restrictions.Eq("SeiyuuID", id))
				.UniqueResult<AniDB_Seiyuu>();
			return cr;
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					AniDB_Seiyuu cr = GetByID(id);
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
