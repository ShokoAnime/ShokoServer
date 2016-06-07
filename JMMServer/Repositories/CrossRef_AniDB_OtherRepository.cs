using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class CrossRef_AniDB_OtherRepository
    {
        public void Save(CrossRef_AniDB_Other obj)
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

        public CrossRef_AniDB_Other GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<CrossRef_AniDB_Other>(id);
            }
        }

        public CrossRef_AniDB_Other GetByAnimeIDAndType(int animeID, CrossRefType xrefType)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByAnimeIDAndType(session, animeID, xrefType);
            }
        }

        public CrossRef_AniDB_Other GetByAnimeIDAndType(ISession session, int animeID, CrossRefType xrefType)
        {
            var cr = session
                .CreateCriteria(typeof(CrossRef_AniDB_Other))
                .Add(Restrictions.Eq("AnimeID", animeID))
                .Add(Restrictions.Eq("CrossRefType", (int)xrefType))
                .UniqueResult<CrossRef_AniDB_Other>();
            return cr;
        }

        public List<CrossRef_AniDB_Other> GetByType(CrossRefType xrefType)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var xrefs = session
                    .CreateCriteria(typeof(CrossRef_AniDB_Other))
                    .Add(Restrictions.Eq("CrossRefType", (int)xrefType))
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

        public List<CrossRef_AniDB_Other> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var series = session
                    .CreateCriteria(typeof(CrossRef_AniDB_Other))
                    .List<CrossRef_AniDB_Other>();

                return new List<CrossRef_AniDB_Other>(series);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    var cr = GetByID(id);
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