using System.Collections.Generic;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class CrossRef_AniDB_OtherRepository : BaseDirectRepository<CrossRef_AniDB_Other,int>
    {

        public CrossRef_AniDB_Other GetByAnimeIDAndType(int animeID, CrossRefType xrefType)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByAnimeIDAndType(session.Wrap(), animeID, xrefType);
            }
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
            using (var session = JMMService.SessionFactory.OpenSession())
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
            using (var session = JMMService.SessionFactory.OpenSession())
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