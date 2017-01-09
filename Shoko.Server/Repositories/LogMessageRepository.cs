using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernateTest;
using JMMServer.Entities;
using NHibernate.Criterion;
using NHibernate;


namespace JMMServer.Repositories
{
	public class LogMessageRepository
	{
		public void Save(ISession session, LogMessage obj)
		{

			// populate the database
			using (var transaction = session.BeginTransaction())
			{
				session.SaveOrUpdate(obj);
				transaction.Commit();
			}
		}

		public LogMessage GetByID(ISession session, int id)
		{
			return session.Get<LogMessage>(id);
		}

		public List<LogMessage> GetByLogType(ISession session, string logType)
		{
			var objs = session
				.CreateCriteria(typeof(LogMessage))
				.Add(Restrictions.Eq("LogType", logType))
				.AddOrder(Order.Desc("LogDate"))
				.List<LogMessage>();
			return new List<LogMessage>(objs);
		}

		public List<LogMessage> GetAll(ISession session)
		{
			var objs = session
				.CreateCriteria(typeof(LogMessage))
				.AddOrder(Order.Desc("LogDate"))
				.List<LogMessage>();

			return new List<LogMessage>(objs);
		}

		public void Delete(ISession session, int id)
		{
			// populate the database
			using (var transaction = session.BeginTransaction())
			{
				LogMessage cr = GetByID(session, id);
				if (cr != null)
				{
					session.Delete(cr);
					transaction.Commit();
				}
			}
		}
	}
}
