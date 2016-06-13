using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class CrossRef_AniDB_MALRepository
    {
        public void Save(CrossRef_AniDB_MAL obj)
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

        public CrossRef_AniDB_MAL GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<CrossRef_AniDB_MAL>(id);
            }
        }

        public List<CrossRef_AniDB_MAL> GetByAnimeID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session, id);
            }
        }

        public List<CrossRef_AniDB_MAL> GetByAnimeID(ISession session, int id)
        {
            var xrefs = session
                .CreateCriteria(typeof(CrossRef_AniDB_MAL))
                .Add(Restrictions.Eq("AnimeID", id))
                .AddOrder(Order.Asc("StartEpisodeType"))
                .AddOrder(Order.Asc("StartEpisodeNumber"))
                .List<CrossRef_AniDB_MAL>();

            return new List<CrossRef_AniDB_MAL>(xrefs);
        }

        public CrossRef_AniDB_MAL GetByMALID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                CrossRef_AniDB_MAL cr = session
                    .CreateCriteria(typeof(CrossRef_AniDB_MAL))
                    .Add(Restrictions.Eq("MALID", id))
                    .UniqueResult<CrossRef_AniDB_MAL>();
                return cr;
            }
        }

        public CrossRef_AniDB_MAL GetByAnimeConstraint(int animeID, int epType, int epNumber)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                CrossRef_AniDB_MAL cr = session
                    .CreateCriteria(typeof(CrossRef_AniDB_MAL))
                    .Add(Restrictions.Eq("AnimeID", animeID))
                    .Add(Restrictions.Eq("StartEpisodeType", epType))
                    .Add(Restrictions.Eq("StartEpisodeNumber", epNumber))
                    .AddOrder(Order.Asc("StartEpisodeType"))
                    .AddOrder(Order.Asc("StartEpisodeNumber"))
                    .UniqueResult<CrossRef_AniDB_MAL>();
                return cr;
            }
        }

        public List<CrossRef_AniDB_MAL> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var xrefs = session
                    .CreateCriteria(typeof(CrossRef_AniDB_MAL))
                    .List<CrossRef_AniDB_MAL>();

                return new List<CrossRef_AniDB_MAL>(xrefs);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    CrossRef_AniDB_MAL cr = GetByID(id);
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