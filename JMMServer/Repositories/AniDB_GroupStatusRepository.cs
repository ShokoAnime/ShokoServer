using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;
using NLog;

namespace JMMServer.Repositories
{
	public class AniDB_GroupStatusRepository
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public void Save(AniDB_GroupStatus obj)
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

		public AniDB_GroupStatus GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<AniDB_GroupStatus>(id);
			}
		}

		public AniDB_GroupStatus GetByAnimeIDAndGroupID(int animeid, int groupid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				AniDB_GroupStatus cr = session
					.CreateCriteria(typeof(AniDB_GroupStatus))
					.Add(Restrictions.Eq("AnimeID", animeid))
					.Add(Restrictions.Eq("GroupID", groupid))
					.UniqueResult<AniDB_GroupStatus>();
				return cr;
			}
		}

		public List<AniDB_GroupStatus> GetByAnimeID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(AniDB_GroupStatus))
					.Add(Restrictions.Eq("AnimeID", id))
					.List<AniDB_GroupStatus>();

				return new List<AniDB_GroupStatus>(objs);
			}
		}

		public void DeleteForAnime(int animeid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					List<AniDB_GroupStatus> grpStatuses = GetByAnimeID(animeid);
					foreach (AniDB_GroupStatus grp in grpStatuses)
						Delete(grp.AniDB_GroupStatusID);
					
				}
			}
		}

		public void Delete(int id)
		{
			int animeID = 0;
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					AniDB_GroupStatus cr = GetByID(id);
					if (cr != null)
					{
						animeID = cr.AnimeID;
						session.Delete(cr);
						transaction.Commit();
					}
				}
			}

			if (animeID > 0)
			{
				logger.Trace("Updating group stats by anime from AniDB_GroupStatusRepository.Delete: {0}", animeID);
				StatsCache.Instance.UpdateUsingAnime(animeID);
			}
		}
	}
}
