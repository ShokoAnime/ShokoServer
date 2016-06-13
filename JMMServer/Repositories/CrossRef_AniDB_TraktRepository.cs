using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class CrossRef_AniDB_TraktRepository
    {
        public void Save(CrossRef_AniDB_Trakt obj)
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

        public CrossRef_AniDB_Trakt GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<CrossRef_AniDB_Trakt>(id);
            }
        }

        public CrossRef_AniDB_Trakt GetByAnimeID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session, id);
            }
        }

        public CrossRef_AniDB_Trakt GetByAnimeID(ISession session, int id)
        {
            CrossRef_AniDB_Trakt cr = session
                .CreateCriteria(typeof(CrossRef_AniDB_Trakt))
                .Add(Restrictions.Eq("AnimeID", id))
                .UniqueResult<CrossRef_AniDB_Trakt>();
            return cr;
        }

        public CrossRef_AniDB_Trakt GetByTraktID(string id, int season)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                CrossRef_AniDB_Trakt cr = session
                    .CreateCriteria(typeof(CrossRef_AniDB_Trakt))
                    .Add(Restrictions.Eq("TraktID", id))
                    .Add(Restrictions.Eq("TraktSeasonNumber", season))
                    .UniqueResult<CrossRef_AniDB_Trakt>();
                return cr;
            }
        }

        public List<CrossRef_AniDB_Trakt> GetByTraktID(string id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var series = session
                    .CreateCriteria(typeof(CrossRef_AniDB_Trakt))
                    .Add(Restrictions.Eq("TraktID", id))
                    .List<CrossRef_AniDB_Trakt>();

                return new List<CrossRef_AniDB_Trakt>(series);
            }
        }

        public List<CrossRef_AniDB_Trakt> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var series = session
                    .CreateCriteria(typeof(CrossRef_AniDB_Trakt))
                    .List<CrossRef_AniDB_Trakt>();

                return new List<CrossRef_AniDB_Trakt>(series);
            }
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    CrossRef_AniDB_Trakt cr = GetByID(id);
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