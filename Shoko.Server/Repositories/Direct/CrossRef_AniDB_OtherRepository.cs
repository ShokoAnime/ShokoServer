using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Server.Collections;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct
{
    public class CrossRef_AniDB_OtherRepository : BaseDirectRepository<CrossRef_AniDB_Other,int>
    {
        private CrossRef_AniDB_OtherRepository()
        {
            
        }

        public static CrossRef_AniDB_OtherRepository Create()
        {
            return new CrossRef_AniDB_OtherRepository();
        }

        public CrossRef_AniDB_Other GetByAnimeIDAndType(int animeID, CrossRefType xrefType)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByAnimeIDAndType(session.Wrap(), animeID, xrefType);
            }
        }

        /// <summary>
        /// Gets other cross references by anime ID.
        /// </summary>
        /// <param name="session">The NHibernate session.</param>
        /// <param name="animeIds">An optional list of anime IDs whose cross references are to be retrieved.
        /// Can be <c>null</c> to get cross references for ALL anime.</param>
        /// <param name="xrefTypes">The types of cross references to find.</param>
        /// <returns>A <see cref="ILookup{TKey,TElement}"/> that maps anime ID to their associated other cross references.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is <c>null</c>.</exception>
        public ILookup<int, CrossRef_AniDB_Other> GetByAnimeIDsAndType(ISessionWrapper session, IReadOnlyCollection<int> animeIds,
            params CrossRefType[] xrefTypes)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (xrefTypes == null || xrefTypes.Length == 0 || animeIds?.Count == 0)
            {
                return EmptyLookup<int, CrossRef_AniDB_Other>.Instance;
            }

            ICriteria criteria = session.CreateCriteria<CrossRef_AniDB_Other>()
                .Add(Restrictions.In(nameof(CrossRef_AniDB_Other.CrossRefType), xrefTypes));

            if (animeIds != null)
            {
                criteria = criteria.Add(Restrictions.InG(nameof(CrossRef_AniDB_Other.AnimeID), animeIds));
            }

            var crossRefs = criteria.List<CrossRef_AniDB_Other>()
                .ToLookup(cr => cr.AnimeID);

            return crossRefs;
        }

        public CrossRef_AniDB_Other GetByAnimeIDAndType(ISessionWrapper session, int animeID, CrossRefType xrefType)
        {
            CrossRef_AniDB_Other cr = session
                .CreateCriteria(typeof(CrossRef_AniDB_Other))
                .Add(Restrictions.Eq("AnimeID", animeID))
                .Add(Restrictions.Eq("CrossRefType", (int) xrefType))
                .UniqueResult<CrossRef_AniDB_Other>();
            return cr;
        }

        public List<CrossRef_AniDB_Other> GetByType(CrossRefType xrefType)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var xrefs = session
                    .CreateCriteria(typeof(CrossRef_AniDB_Other))
                    .Add(Restrictions.Eq("CrossRefType", (int) xrefType))
                    .List<CrossRef_AniDB_Other>();

                return new List<CrossRef_AniDB_Other>(xrefs);
            }
        }

        public List<CrossRef_AniDB_Other> GetByAnimeID(int animeID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var xrefs = session
                    .CreateCriteria(typeof(CrossRef_AniDB_Other))
                    .Add(Restrictions.Eq("AnimeID", animeID))
                    .List<CrossRef_AniDB_Other>();

                return new List<CrossRef_AniDB_Other>(xrefs);
            }
        }
    }
}