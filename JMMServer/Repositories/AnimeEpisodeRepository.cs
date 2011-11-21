using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using NHibernate.Criterion;
using BinaryNorthwest;

namespace JMMServer.Repositories
{
	public class AnimeEpisodeRepository
	{
		public void Save(AnimeEpisode obj)
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

		public AnimeEpisode GetByID(int id)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				return session.Get<AnimeEpisode>(id);
			}
		}

		public List<AnimeEpisode> GetBySeriesID(int seriesid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var eps = session
					.CreateCriteria(typeof(AnimeEpisode))
					.Add(Restrictions.Eq("AnimeSeriesID", seriesid))
					.List<AnimeEpisode>();

				return new List<AnimeEpisode>(eps);
			}
		}

		public AnimeEpisode GetByAniDBEpisodeID(int epid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				AnimeEpisode obj = session
					.CreateCriteria(typeof(AnimeEpisode))
					.Add(Restrictions.Eq("AniDB_EpisodeID", epid))
					.UniqueResult<AnimeEpisode>();

				return obj;
			}
		}

		public List<AnimeEpisode> GetByAniEpisodeIDAndSeriesID(int epid, int seriesid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var eps = session
					.CreateCriteria(typeof(AnimeEpisode))
					.Add(Restrictions.Eq("AniDB_EpisodeID", epid))
					.Add(Restrictions.Eq("AnimeSeriesID", seriesid))
					.List<AnimeEpisode>();

				return new List<AnimeEpisode>(eps);
			}
		}

		/// <summary>
		/// Get all the AnimeEpisode records associate with an AniDB_File record
		/// AnimeEpisode.AniDB_EpisodeID -> AniDB_Episode.EpisodeID
		/// AniDB_Episode.EpisodeID -> CrossRef_File_Episode.EpisodeID
		/// CrossRef_File_Episode.Hash -> VideoLocal.Hash
		/// </summary>
		/// <param name="hash"></param>
		/// <returns></returns>
		public List<AnimeEpisode> GetByHash(string hash)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var eps = session.CreateQuery("FROM AnimeEpisode ae WHERE ae.AniDB_EpisodeID IN (Select EpisodeID FROM CrossRef_File_Episode xref WHERE xref.Hash= :Hash)")
					.SetParameter("Hash", hash)
					.List<AnimeEpisode>();

				/*var eps = session
					.CreateCriteria(typeof(AnimeEpisode))
					.Add(Restrictions.Eq("AniDB_EpisodeID", epid))
					.Add(Restrictions.Eq("AnimeSeriesID", seriesid))
					.List<AnimeEpisode>();*/

				return new List<AnimeEpisode>(eps);
			}
		}

		public List<AnimeEpisode> GetEpisodesWithMultipleFiles()
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var eps = session.CreateQuery("FROM AnimeEpisode x WHERE x.AniDB_EpisodeID IN (Select xref.EpisodeID FROM CrossRef_File_Episode xref WHERE xref.Hash IN (Select vl.Hash from VideoLocal vl) GROUP BY xref.EpisodeID HAVING COUNT(xref.EpisodeID) > 1)")
					.List<AnimeEpisode>();

				return new List<AnimeEpisode>(eps);
			}
		}

		public List<AnimeEpisode> GetUnwatchedEpisodes(int seriesid, int userid)
		{
			using (var session = JMMService.SessionFactory.OpenSession())
			{
				var eps = session.CreateQuery("FROM AnimeEpisode x WHERE x.AnimeEpisodeID NOT IN (SELECT AnimeEpisodeID FROM AnimeEpisode_User WHERE AnimeSeriesID = :AnimeSeriesID AND JMMUserID = :JMMUserID) AND x.AnimeSeriesID = :AnimeSeriesID")
					.SetParameter("AnimeSeriesID", seriesid)
					.SetParameter("JMMUserID", userid)
					.List<AnimeEpisode>();

				return new List<AnimeEpisode>(eps);
			}
		}

		public void Delete(int id)
		{
			AnimeEpisode cr = GetByID(id);
			if (cr != null)
			{
				// delete user records
				AnimeEpisode_UserRepository repUsers = new AnimeEpisode_UserRepository();
				foreach (AnimeEpisode_User epuser in repUsers.GetByEpisodeID(id))
					repUsers.Delete(epuser.AnimeEpisode_UserID);
			}

			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// populate the database
				using (var transaction = session.BeginTransaction())
				{
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
