using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
	public class AniDB_Character_CreatorRepository
	{
		public void Save(AniDB_Character_Creator obj)
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

		public AniDB_Character_Creator GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<AniDB_Character_Creator>(id);
			}
		}

		public AniDB_Character_Creator GetByCharIDAndCreatorID(int animeid, int catid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				AniDB_Character_Creator cr = session
					.CreateCriteria(typeof(AniDB_Character_Creator))
					.Add(Restrictions.Eq("CharID", animeid))
					.Add(Restrictions.Eq("CreatorID", catid))
					.UniqueResult<AniDB_Character_Creator>();
				return cr;
			}
		}

		public List<AniDB_Character_Creator> GetByCharID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(AniDB_Character_Creator))
					.Add(Restrictions.Eq("CharID", id))
					.List<AniDB_Character_Creator>();

				return new List<AniDB_Character_Creator>(objs);
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					AniDB_Character_Creator cr = GetByID(id);
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
