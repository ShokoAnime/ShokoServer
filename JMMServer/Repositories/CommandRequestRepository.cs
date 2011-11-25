using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate.Criterion;
using JMMServer;
using JMMServer.Entities;

namespace JMMServer.Repositories
{
	public class CommandRequestRepository
	{
		public void Save(CommandRequest obj)
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

		public CommandRequest GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<CommandRequest>(id);
				/*CommandRequest cr = session
					.CreateCriteria(typeof(CommandRequest))
					.Add(Restrictions.Eq("CommandRequestID", id))
					.UniqueResult<CommandRequest>();
				return cr;*/
			}
		}

		public CommandRequest GetByCommandID(string cmdid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				CommandRequest cr = session
					.CreateCriteria(typeof(CommandRequest))
					.Add(Restrictions.Eq("CommandID", cmdid))
					.UniqueResult<CommandRequest>();
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
					CommandRequest cr = GetByID(id);
					if (cr != null)
					{
						session.Delete(cr);
						transaction.Commit();
					}
				}
			}
		}

		public CommandRequest GetNextDBCommandRequestGeneral()
		{
			/*SELECT TOP 1 CommandRequestID
			FROM CommandRequest
			ORDER BY Priority ASC, DateTimeUpdated ASC*/
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				IList<CommandRequest> crs = session
					.CreateCriteria(typeof(CommandRequest))
					.Add(!Restrictions.Eq("CommandType", (int)CommandRequestType.HashFile))
					.Add(!Restrictions.Eq("CommandType", (int)CommandRequestType.ImageDownload))
					.AddOrder(Order.Asc("Priority"))
					.AddOrder(Order.Asc("DateTimeUpdated"))
					.SetMaxResults(1)
					.List<CommandRequest>();

				if (crs.Count > 0) return crs[0];

				return null;
			}
		}

		public CommandRequest GetNextDBCommandRequestHasher()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				IList<CommandRequest> crs = session
					.CreateCriteria(typeof(CommandRequest))
					.Add(Restrictions.Eq("CommandType", (int)CommandRequestType.HashFile))
					.AddOrder(Order.Asc("Priority"))
					.AddOrder(Order.Asc("DateTimeUpdated"))
					.SetMaxResults(1)
					.List<CommandRequest>();

				if (crs.Count > 0) return crs[0];

				return null;
			}
		}

		public CommandRequest GetNextDBCommandRequestImages()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				IList<CommandRequest> crs = session
					.CreateCriteria(typeof(CommandRequest))
					.Add(Restrictions.Eq("CommandType", (int)CommandRequestType.ImageDownload))
					.AddOrder(Order.Asc("Priority"))
					.AddOrder(Order.Asc("DateTimeUpdated"))
					.SetMaxResults(1)
					.List<CommandRequest>();

				if (crs.Count > 0) return crs[0];

				return null;
			}
		}

		public int GetQueuedCommandCountGeneral()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var cnt = session
					.CreateCriteria(typeof(CommandRequest))
					.Add(!Restrictions.Eq("CommandType", (int)CommandRequestType.HashFile))
					.Add(!Restrictions.Eq("CommandType", (int)CommandRequestType.ImageDownload))
					.SetProjection(Projections.Count("CommandRequestID")).UniqueResult();

				return (int)cnt;
			}
		}

		public int GetQueuedCommandCountHasher()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var cnt = session
					.CreateCriteria(typeof(CommandRequest))
					.Add(Restrictions.Eq("CommandType", (int)CommandRequestType.HashFile))
					.SetProjection(Projections.Count("CommandRequestID")).UniqueResult();

				return (int)cnt;
			}
		}

		public int GetQueuedCommandCountImages()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var cnt = session
					.CreateCriteria(typeof(CommandRequest))
					.Add(Restrictions.Eq("CommandType", (int)CommandRequestType.ImageDownload))
					.SetProjection(Projections.Count("CommandRequestID")).UniqueResult();

				return (int)cnt;
			}
		}

		public List<CommandRequest> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var series = session
					.CreateCriteria(typeof(CommandRequest))
					.List<CommandRequest>();

				return new List<CommandRequest>(series);
			}
		}
	}
}
