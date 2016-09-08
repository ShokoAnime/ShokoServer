using System.Collections.Generic;
using AniDBAPI;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class AniDB_EpisodeRepository
    {
        public void Save(AniDB_Episode obj)
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

        public AniDB_Episode GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AniDB_Episode>(id);
            }
        }

        public AniDB_Episode GetByEpisodeID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AniDB_Episode cr = session
                    .CreateCriteria(typeof(AniDB_Episode))
                    .Add(Restrictions.Eq("EpisodeID", id))
                    .UniqueResult<AniDB_Episode>();
                return cr;
            }
        }

        public List<AniDB_Episode> GetByAnimeID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session.Wrap(), id);
            }
        }

        public List<AniDB_Episode> GetByAnimeID(ISessionWrapper session, int id)
        {
            var eps = session
                .CreateCriteria(typeof(AniDB_Episode))
                .Add(Restrictions.Eq("AnimeID", id))
                .List<AniDB_Episode>();

            return new List<AniDB_Episode>(eps);
        }

        public List<AniDB_Episode> GetByAnimeIDAndEpisodeNumber(int animeid, int epnumber)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var eps = session
                    .CreateCriteria(typeof(AniDB_Episode))
                    .Add(Restrictions.Eq("AnimeID", animeid))
                    .Add(Restrictions.Eq("EpisodeNumber", epnumber))
                    .Add(Restrictions.Eq("EpisodeType", (int) enEpisodeType.Episode))
                    .List<AniDB_Episode>();

                return new List<AniDB_Episode>(eps);
            }
        }

        public List<AniDB_Episode> GetByAnimeIDAndEpisodeTypeNumber(int animeid, enEpisodeType epType, int epnumber)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var eps = session
                    .CreateCriteria(typeof(AniDB_Episode))
                    .Add(Restrictions.Eq("AnimeID", animeid))
                    .Add(Restrictions.Eq("EpisodeNumber", epnumber))
                    .Add(Restrictions.Eq("EpisodeType", (int) epType))
                    .List<AniDB_Episode>();

                return new List<AniDB_Episode>(eps);
            }
        }

        public List<AniDB_Episode> GetEpisodesWithMultipleFiles()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var eps =
                    session.CreateQuery(
                        "FROM AniDB_Episode x WHERE x.EpisodeID IN (Select xref.EpisodeID FROM CrossRef_File_Episode xref GROUP BY xref.EpisodeID HAVING COUNT(xref.EpisodeID) > 1)")
                        .List<AniDB_Episode>();

                return new List<AniDB_Episode>(eps);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    AniDB_Episode cr = GetByID(id);
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