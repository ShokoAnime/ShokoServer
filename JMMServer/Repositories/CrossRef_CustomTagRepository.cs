using System.Collections.Generic;
using System.Linq;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class CrossRef_CustomTagRepository
    {
        private static PocoCache<int, CrossRef_CustomTag> Cache;
        private static PocoIndex<int, CrossRef_CustomTag, int> Tags;
        private static PocoIndex<int, CrossRef_CustomTag, int,int> Refs;

        public static void InitCache()
        {
            string t = "CrossRef_CustomTags";
            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t, string.Empty);
            CrossRef_CustomTagRepository repo = new CrossRef_CustomTagRepository();
            Cache = new PocoCache<int, CrossRef_CustomTag>(repo.InternalGetAll(), a => a.CrossRef_CustomTagID);
            Tags = new PocoIndex<int, CrossRef_CustomTag, int>(Cache, a => a.CustomTagID);
            Refs = new PocoIndex<int, CrossRef_CustomTag, int,int>(Cache, a => a.CrossRefID, a=>a.CrossRefType);
        }
        internal List<CrossRef_CustomTag> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(CrossRef_CustomTag))
                    .List<CrossRef_CustomTag>();

                return new List<CrossRef_CustomTag>(objs);
            }
        }

        public void Save(CrossRef_CustomTag obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    Cache.Update(obj);
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
            return Cache.Values.ToList();
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(CrossRef_CustomTag))
                    .List<CrossRef_CustomTag>();

                return new List<CrossRef_CustomTag>(objs);
                ;
            }*/
        }

        public List<CrossRef_CustomTag> GetByAnimeID(int id)
        {
            return Refs.GetMultiple(id, (int) CustomTagCrossRefType.Anime);
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session, id);
            }*/
        }

        public List<CrossRef_CustomTag> GetByAnimeID(ISession session, int id)
        {
            return Refs.GetMultiple(id, (int)CustomTagCrossRefType.Anime);

            /*
            var tags = session
                .CreateCriteria(typeof(CrossRef_CustomTag))
                .Add(Restrictions.Eq("CrossRefID", id))
                .Add(Restrictions.Eq("CrossRefType", (int) CustomTagCrossRefType.Anime))
                .List<CrossRef_CustomTag>();

            return new List<CrossRef_CustomTag>(tags);*/
        }

        public List<CrossRef_CustomTag> GetByCustomTagID(int id)
        {
            return Tags.GetMultiple(id);
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByCustomTagID(session, id);
            }*/
        }

        public List<CrossRef_CustomTag> GetByCustomTagID(ISession session, int id)
        {
            return Tags.GetMultiple(id);
            /*
            var tags = session
                .CreateCriteria(typeof(CrossRef_CustomTag))
                .Add(Restrictions.Eq("CustomTagID", id))
                .List<CrossRef_CustomTag>();

            return new List<CrossRef_CustomTag>(tags);*/
        }

        public List<CrossRef_CustomTag> GetByUniqueID(int customTagID, int crossRefType, int crossRefID)
        {
            return Refs.GetMultiple(crossRefID, crossRefType).Where(a => a.CustomTagID == customTagID).ToList();
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var tags = session
                    .CreateCriteria(typeof(CrossRef_CustomTag))
                    .Add(Restrictions.Eq("CustomTagID", customTagID))
                    .Add(Restrictions.Eq("CrossRefType", crossRefType))
                    .Add(Restrictions.Eq("CrossRefID", crossRefID))
                    .List<CrossRef_CustomTag>();

                return new List<CrossRef_CustomTag>(tags);
            }*/
        }

        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    CrossRef_CustomTag cr = GetByID(id);
                    if (cr != null)
                    {
                        Cache.Remove(cr);
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
    }
}