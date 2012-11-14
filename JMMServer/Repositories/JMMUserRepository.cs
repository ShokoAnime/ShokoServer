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
	public class JMMUserRepository
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public void Save(JMMUser obj)
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
            logger.Trace("Updating group filter stats by user from JMMUserRepository.Save: {0}", obj.JMMUserID);
            StatsCache.Instance.UpdateGroupFilterUsingUser(obj.JMMUserID);
		}

		public JMMUser GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetByID(session, id);
			}
		}

		public JMMUser GetByID(ISession session, int id)
		{
			return session.Get<JMMUser>(id);
		}

		public List<JMMUser> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return GetAll(session);
			}
		}

		public List<JMMUser> GetAll(ISession session)
		{
			var objs = session
				.CreateCriteria(typeof(JMMUser))
				.List<JMMUser>();

			return new List<JMMUser>(objs);
		}

		public List<JMMUser> GetAniDBUsers()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(JMMUser))
					.Add(Restrictions.Eq("IsAniDBUser", 1))
					.List<JMMUser>();

				return new List<JMMUser>(objs);
			}
		}

		public List<JMMUser> GetTraktUsers()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(JMMUser))
					.Add(Restrictions.Eq("IsTraktUser", 1))
					.List<JMMUser>();

				return new List<JMMUser>(objs);
			}
		}

		public JMMUser AuthenticateUser(string userName, string password)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				string hashedPassword = Digest.Hash(password);
				JMMUser cr = session
					.CreateCriteria(typeof(JMMUser))
					.Add(Restrictions.Eq("Username", userName))
					.Add(Restrictions.Eq("Password", hashedPassword))
					.UniqueResult<JMMUser>();
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
					JMMUser cr = GetByID(id);
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
