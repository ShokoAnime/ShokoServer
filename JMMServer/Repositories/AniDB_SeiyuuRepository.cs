using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class AniDB_SeiyuuRepository
    {
        public void Save(AniDB_Seiyuu obj)
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

        public AniDB_Seiyuu GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AniDB_Seiyuu>(id);
            }
        }

        public List<AniDB_Seiyuu> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_Seiyuu))
                    .List<AniDB_Seiyuu>();

                return new List<AniDB_Seiyuu>(objs);
            }
        }

        public AniDB_Seiyuu GetBySeiyuuID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var cr = session
                    .CreateCriteria(typeof(AniDB_Seiyuu))
                    .Add(Restrictions.Eq("SeiyuuID", id))
                    .UniqueResult<AniDB_Seiyuu>();
                return cr;
            }
        }

        public AniDB_Seiyuu GetBySeiyuuID(ISession session, int id)
        {
            var cr = session
                .CreateCriteria(typeof(AniDB_Seiyuu))
                .Add(Restrictions.Eq("SeiyuuID", id))
                .UniqueResult<AniDB_Seiyuu>();
            return cr;
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