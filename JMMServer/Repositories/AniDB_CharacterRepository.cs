using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class AniDB_CharacterRepository
    {
        public void Save(AniDB_Character obj)
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

        public AniDB_Character GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AniDB_Character>(id);
            }
        }

        public List<AniDB_Character> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_Character))
                    .List<AniDB_Character>();

                return new List<AniDB_Character>(objs);
            }
        }

        public AniDB_Character GetByCharID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByCharID(session, id);
            }
        }

        public AniDB_Character GetByCharID(ISession session, int id)
        {
            var cr = session
                .CreateCriteria(typeof(AniDB_Character))
                .Add(Restrictions.Eq("CharID", id))
                .UniqueResult<AniDB_Character>();
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