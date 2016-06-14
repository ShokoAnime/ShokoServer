using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
	public class FileNameHashRepository
	{
		public void Save(FileNameHash obj)
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

		public FileNameHash GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<FileNameHash>(id);
			}
		}

		public List<FileNameHash> GetByHash(string hash)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var xrefs = session
					.CreateCriteria(typeof(FileNameHash))
					.Add(Restrictions.Eq("Hash", hash))
					.List<FileNameHash>();

				return new List<FileNameHash>(xrefs);
			}
		}

		public List<FileNameHash> GetByFileNameAndSize(string filename, long filesize)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var fnhashes = session
					.CreateCriteria(typeof(FileNameHash))
					.Add(Restrictions.Eq("FileName", filename))
					.Add(Restrictions.Eq("FileSize", filesize))
					.List<FileNameHash>();

				return new List<FileNameHash>(fnhashes);
			}
		}

		public FileNameHash GetByNameSizeAndHash(string filename, long filesize, string hash)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				FileNameHash fnhash = session
					.CreateCriteria(typeof(FileNameHash))
					.Add(Restrictions.Eq("Hash", hash))
					.Add(Restrictions.Eq("FileName", filename))
					.Add(Restrictions.Eq("FileSize", filesize))
					.UniqueResult<FileNameHash>();

				return fnhash;
			}
		}

		public void Delete(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					FileNameHash cr = GetByID(id);
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
