using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class CrossRef_AniDB_MALRepository : BaseDirectRepository<CrossRef_AniDB_MAL, int>
    {
        private CrossRef_AniDB_MALRepository()
        {
            
        }

        public static CrossRef_AniDB_MALRepository Create()
        {
            return new CrossRef_AniDB_MALRepository();
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
    }
}