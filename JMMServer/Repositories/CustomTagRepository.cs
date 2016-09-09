using System.Collections.Generic;
using System.Linq;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class CustomTagRepository
    {
        private static PocoCache<int, CustomTag> Cache;

        public static void InitCache()
        {
            string t = "AniDB_Anime_Tag";
            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t, string.Empty);
            CustomTagRepository repo = new CustomTagRepository();
            Cache = new PocoCache<int, CustomTag> (repo.InternalGetAll(), a => a.CustomTagID);
        }
        internal List<CustomTag> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(CustomTag))
                    .List<CustomTag>();

                return new List<CustomTag>(objs);
                ;
            }
        }

        public void Save(CustomTag obj)
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

        public CustomTag GetByID(int id)
        {
            return Cache.Get(id);
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<CustomTag>(id);
            }*/
        }

        public List<CustomTag> GetAll()
        {
            return Cache.Values.ToList();
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(CustomTag))
                    .List<CustomTag>();

                return new List<CustomTag>(objs);
                ;
            }*/
        }

        public List<CustomTag> GetByAnimeID(int animeID)
        {
            return
                new CrossRef_CustomTagRepository().GetByAnimeID(animeID)
                    .Select(a => GetByID(a.CustomTagID))
                    .Where(a => a != null)
                    .ToList();
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session, animeID);
            }*/
        }

        public List<CustomTag> GetByAnimeID(ISession session, int animeID)
        {
            return new CrossRef_CustomTagRepository().GetByAnimeID(animeID)
                .Select(a => GetByID(a.CustomTagID))
                .Where(a => a != null)
                .ToList();

            /*
            var tags =
                session.CreateQuery(
                    "Select tag FROM CustomTag as tag, CrossRef_CustomTag as xref WHERE tag.CustomTagID = xref.CustomTagID AND xref.CrossRefID= :animeID AND xref.CrossRefType= :xrefType")
                    .SetParameter("animeID", animeID)
                    .SetParameter("xrefType", (int) CustomTagCrossRefType.Anime)
                    .List<CustomTag>();

            return new List<CustomTag>(tags);*/
        }


        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    CustomTag cr = GetByID(id);
                    if (cr != null)
                    {
                        Cache.Remove(cr);
                        // first delete any cross ref records 
                        CrossRef_CustomTagRepository repXrefs = new CrossRef_CustomTagRepository();
                        foreach (CrossRef_CustomTag xref in repXrefs.GetByCustomTagID(session, id))
                            repXrefs.Delete(xref.CrossRef_CustomTagID);

                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
    }
}