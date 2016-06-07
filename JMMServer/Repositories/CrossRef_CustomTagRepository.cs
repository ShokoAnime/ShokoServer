using System.Collections.Generic;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class CrossRef_CustomTagRepository
    {
        public void Save(CrossRef_CustomTag obj)
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

        public CrossRef_CustomTag GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<CrossRef_CustomTag>(id);
            }
        }

        public List<CrossRef_CustomTag> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(CrossRef_CustomTag))
                    .List<CrossRef_CustomTag>();

                return new List<CrossRef_CustomTag>(objs);
                ;
            }
        }

        public List<CrossRef_CustomTag> GetByAnimeID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session, id);
            }
        }

        public List<CrossRef_CustomTag> GetByAnimeID(ISession session, int id)
        {
            var tags = session
                .CreateCriteria(typeof(CrossRef_CustomTag))
                .Add(Restrictions.Eq("CrossRefID", id))
                .Add(Restrictions.Eq("CrossRefType", (int)CustomTagCrossRefType.Anime))
                .List<CrossRef_CustomTag>();

            return new List<CrossRef_CustomTag>(tags);
        }

        public List<CrossRef_CustomTag> GetByCustomTagID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByCustomTagID(session, id);
            }
        }

        public List<CrossRef_CustomTag> GetByCustomTagID(ISession session, int id)
        {
            var tags = session
                .CreateCriteria(typeof(CrossRef_CustomTag))
                .Add(Restrictions.Eq("CustomTagID", id))
                .List<CrossRef_CustomTag>();

            return new List<CrossRef_CustomTag>(tags);
        }

        public List<CrossRef_CustomTag> GetByUniqueID(int customTagID, int crossRefType, int crossRefID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var tags = session
                    .CreateCriteria(typeof(CrossRef_CustomTag))
                    .Add(Restrictions.Eq("CustomTagID", customTagID))
                    .Add(Restrictions.Eq("CrossRefType", crossRefType))
                    .Add(Restrictions.Eq("CrossRefID", crossRefID))
                    .List<CrossRef_CustomTag>();

                return new List<CrossRef_CustomTag>(tags);
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