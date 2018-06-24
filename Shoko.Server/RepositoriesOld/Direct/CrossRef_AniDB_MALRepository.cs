using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate;
using NHibernate.Criterion;
using NHibernate.Util;
using Shoko.Commons.Collections;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct
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
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
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

        public ILookup<int, CrossRef_AniDB_MAL> GetByAnimeIDs(ISessionWrapper session, int[] animeIds)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (animeIds == null)
                throw new ArgumentNullException(nameof(animeIds));

            if (animeIds.Length == 0)
            {
                return EmptyLookup<int, CrossRef_AniDB_MAL>.Instance;
            }

            var xrefByAnime = session.CreateCriteria<CrossRef_AniDB_MAL>()
                .Add(Restrictions.In(nameof(CrossRef_AniDB_MAL.AnimeID), animeIds))
                .AddOrder(Order.Asc(nameof(CrossRef_AniDB_MAL.StartEpisodeType)))
                .AddOrder(Order.Asc(nameof(CrossRef_AniDB_MAL.StartEpisodeNumber)))
                .List<CrossRef_AniDB_MAL>()
                .ToLookup(cr => cr.AnimeID);

            return xrefByAnime;
        }

        public CrossRef_AniDB_MAL GetByMALID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                IList<CrossRef_AniDB_MAL> cr = session
                    .CreateCriteria(typeof(CrossRef_AniDB_MAL))
                    .Add(Restrictions.Eq("MALID", id))
                    .List<CrossRef_AniDB_MAL>();
                var xref = cr.FirstOrDefault(a => !string.IsNullOrEmpty(a.MALTitle));
                if (xref != null && cr.Count > 1)
                {
                    cr.Remove(xref);
                    cr.ForEach(Delete);
                }

                return xref ?? cr.FirstOrDefault();
            }
        }

        public List<CrossRef_AniDB_MAL> GetByAnimeConstraint(int animeID, int epType, int epNumber)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                List<CrossRef_AniDB_MAL> cr = session
                    .CreateCriteria(typeof(CrossRef_AniDB_MAL))
                    .Add(Restrictions.Eq("AnimeID", animeID))
                    .Add(Restrictions.Eq("StartEpisodeType", epType))
                    .Add(Restrictions.Eq("StartEpisodeNumber", epNumber))
                    .AddOrder(Order.Asc("StartEpisodeType"))
                    .AddOrder(Order.Asc("StartEpisodeNumber"))
                    .List<CrossRef_AniDB_MAL>().ToList();
                return cr;
            }
        }
    }
}