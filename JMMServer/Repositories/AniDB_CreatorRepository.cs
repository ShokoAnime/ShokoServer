using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
	public class AniDB_CreatorRepository
	{
		public void Save(AniDB_Creator obj)
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

		public AniDB_Creator GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<AniDB_Creator>(id);
			}
		}

		public AniDB_Creator GetByCreatorID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				AniDB_Creator cr = session
					.CreateCriteria(typeof(AniDB_Creator))
					.Add(Restrictions.Eq("CreatorID", id))
					.UniqueResult<AniDB_Creator>();
				return cr;
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					AniDB_Creator cr = GetByID(id);
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
