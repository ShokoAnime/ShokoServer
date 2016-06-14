using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
	public class CrossRef_Languages_AniDB_FileRepository
	{
		public void Save(CrossRef_Languages_AniDB_File obj)
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

		public CrossRef_Languages_AniDB_File GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<CrossRef_Languages_AniDB_File>(id);
			}
		}

		public List<CrossRef_Languages_AniDB_File> GetByFileID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var files = session
					.CreateCriteria(typeof(CrossRef_Languages_AniDB_File))
					.Add(Restrictions.Eq("FileID", id))
					.List<CrossRef_Languages_AniDB_File>();

				return new List<CrossRef_Languages_AniDB_File>(files);
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					CrossRef_Languages_AniDB_File cr = GetByID(id);
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
