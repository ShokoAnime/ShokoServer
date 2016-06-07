using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class IgnoreAnimeRepository
    {
        public void Save(IgnoreAnime obj)
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

        public IgnoreAnime GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<IgnoreAnime>(id);
            }
        }

        public IgnoreAnime GetByAnimeUserType(int animeID, int userID, int ignoreType)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var obj = session
                    .CreateCriteria(typeof(IgnoreAnime))
                    .Add(Restrictions.Eq("AnimeID", animeID))
                    .Add(Restrictions.Eq("JMMUserID", userID))
                    .Add(Restrictions.Eq("IgnoreType", ignoreType))
                    .UniqueResult<IgnoreAnime>();

                return obj;
            }
        }

        public List<IgnoreAnime> GetByUserAndType(int userID, int ignoreType)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(IgnoreAnime))
                    .Add(Restrictions.Eq("JMMUserID", userID))
                    .Add(Restrictions.Eq("IgnoreType", ignoreType))
                    .List<IgnoreAnime>();

                return new List<IgnoreAnime>(objs);
            }
        }

        public List<IgnoreAnime> GetByUser(int userID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(IgnoreAnime))
                    .Add(Restrictions.Eq("JMMUserID", userID))
                    .List<IgnoreAnime>();

                return new List<IgnoreAnime>(objs);
            }
        }


        public List<IgnoreAnime> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var gfcs = session
                    .CreateCriteria(typeof(IgnoreAnime))
                    .List<IgnoreAnime>();
                return new List<IgnoreAnime>(gfcs);
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