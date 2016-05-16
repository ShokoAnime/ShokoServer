using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;
using NHibernate;

namespace JMMServer.Repositories
{
	public class AniDB_VoteRepository
	{
		public void Save(AniDB_Vote obj)
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
			if (obj.VoteType == (int)AniDBVoteType.Anime || obj.VoteType == (int)AniDBVoteType.AnimeTemp)
			{
                AniDB_Anime.UpdateStatsByAnimeID(obj.EntityID);
			}
		}

		public AniDB_Vote GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<AniDB_Vote>(id);
			}
		}

		public List<AniDB_Vote> GetAll()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var objs = session
					.CreateCriteria(typeof(AniDB_Vote))
					.List<AniDB_Vote>();

				return new List<AniDB_Vote>(objs); ;
			}
		}

		public AniDB_Vote GetByEntityAndType(int entID, AniDBVoteType voteType)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				AniDB_Vote cr = session
					.CreateCriteria(typeof(AniDB_Vote))
					.Add(Restrictions.Eq("EntityID", entID))
					.Add(Restrictions.Eq("VoteType", (int)voteType))
					.UniqueResult<AniDB_Vote>();

				return cr;
			}
		}

		public List<AniDB_Vote> GetByEntity(int entID)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var votes = session
					.CreateCriteria(typeof(AniDB_Vote))
					.Add(Restrictions.Eq("EntityID", entID))
					.List<AniDB_Vote>();

				return new List<AniDB_Vote>(votes);
			}
		}

		public AniDB_Vote GetByAnimeID(int animeID)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var votes = session
					.CreateCriteria(typeof(AniDB_Vote))
					.Add(Restrictions.Eq("EntityID", animeID))
					.List<AniDB_Vote>();

				List<AniDB_Vote> tempList = new List<AniDB_Vote>(votes);
				List<AniDB_Vote> retList = new List<AniDB_Vote>();

				foreach (AniDB_Vote vt in tempList)
				{
					if (vt.VoteType == (int)AniDBVoteType.Anime || vt.VoteType == (int)AniDBVoteType.AnimeTemp)
						return vt;
				}

				return null;
			}
		}

		public AniDB_Vote GetByAnimeID(ISession session, int animeID)
		{
			var votes = session
				.CreateCriteria(typeof(AniDB_Vote))
				.Add(Restrictions.Eq("EntityID", animeID))
				.List<AniDB_Vote>();

			List<AniDB_Vote> tempList = new List<AniDB_Vote>(votes);
			List<AniDB_Vote> retList = new List<AniDB_Vote>();

			foreach (AniDB_Vote vt in tempList)
			{
				if (vt.VoteType == (int)AniDBVoteType.Anime || vt.VoteType == (int)AniDBVoteType.AnimeTemp)
					return vt;
			}

			return null;
		}

		public void Delete(int id)
		{
			int? animeID = null;
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
					AniDB_Vote cr = GetByID(id);
					if (cr != null)
					{
						if (cr.VoteType == (int)AniDBVoteType.Anime || cr.VoteType == (int)AniDBVoteType.AnimeTemp)
							animeID = cr.EntityID;

						session.Delete(cr);
						transaction.Commit();
					}
				}
			}
			if (animeID.HasValue)
			{
                AniDB_Anime.UpdateStatsByAnimeID(animeID.Value);
			}
		}
	}
}
