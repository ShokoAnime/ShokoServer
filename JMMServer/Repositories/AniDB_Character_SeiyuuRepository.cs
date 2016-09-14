using System.Collections.Generic;
using System.Linq;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class AniDB_Character_SeiyuuRepository
    {
        public void Save(AniDB_Character_Seiyuu obj)
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
        public void Save(IEnumerable<AniDB_Character_Seiyuu> objs)
        {
            if (!objs.Any())
                return;
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    foreach(AniDB_Character_Seiyuu obj in objs)
                        session.SaveOrUpdate(obj);
                    transaction.Commit();
                }
            }
        }
        public AniDB_Character_Seiyuu GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AniDB_Character_Seiyuu>(id);
            }
        }

        public AniDB_Character_Seiyuu GetByCharIDAndSeiyuuID(int animeid, int catid)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AniDB_Character_Seiyuu cr = session
                    .CreateCriteria(typeof(AniDB_Character_Seiyuu))
                    .Add(Restrictions.Eq("CharID", animeid))
                    .Add(Restrictions.Eq("SeiyuuID", catid))
                    .UniqueResult<AniDB_Character_Seiyuu>();
                return cr;
            }
        }

        public AniDB_Character_Seiyuu GetByCharIDAndSeiyuuID(ISession session, int animeid, int catid)
        {
            AniDB_Character_Seiyuu cr = session
                .CreateCriteria(typeof(AniDB_Character_Seiyuu))
                .Add(Restrictions.Eq("CharID", animeid))
                .Add(Restrictions.Eq("SeiyuuID", catid))
                .UniqueResult<AniDB_Character_Seiyuu>();
            return cr;
        }

        public List<AniDB_Character_Seiyuu> GetByCharID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByCharID(session, id);
            }
        }

        public List<AniDB_Character_Seiyuu> GetByCharID(ISession session, int id)
        {
            var objs = session
                .CreateCriteria(typeof(AniDB_Character_Seiyuu))
                .Add(Restrictions.Eq("CharID", id))
                .List<AniDB_Character_Seiyuu>();

            return new List<AniDB_Character_Seiyuu>(objs);
        }

        public List<AniDB_Character_Seiyuu> GetBySeiyuuID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_Character_Seiyuu))
                    .Add(Restrictions.Eq("SeiyuuID", id))
                    .List<AniDB_Character_Seiyuu>();

                return new List<AniDB_Character_Seiyuu>(objs);
            }
        }

        public void Delete(int id)
        {

            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    AniDB_Character_Seiyuu cr = GetByID(id);
                    if (cr != null)
                    {
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
        public void Delete(IEnumerable<AniDB_Character_Seiyuu> seiyuus)
        {
            if (!seiyuus.Any())
                return;
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    foreach (AniDB_Character_Seiyuu cr in seiyuus)
                        session.Delete(cr);
                    transaction.Commit();
                }
            }
        }

    }
}