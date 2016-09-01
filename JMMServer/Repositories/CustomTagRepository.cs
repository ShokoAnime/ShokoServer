using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Collections;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class CustomTagRepository
    {
        public void Save(CustomTag obj)
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

        public CustomTag GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<CustomTag>(id);
            }
        }

        public List<CustomTag> GetAll()
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

        public List<CustomTag> GetByAnimeID(int animeID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session.Wrap(), animeID);
            }
        }

        public List<CustomTag> GetByAnimeID(ISessionWrapper session, int animeID)
        {
            var tags =
                session.CreateQuery(
                    "Select tag FROM CustomTag as tag, CrossRef_CustomTag as xref WHERE tag.CustomTagID = xref.CustomTagID AND xref.CrossRefID= :animeID AND xref.CrossRefType= :xrefType")
                    .SetParameter("animeID", animeID)
                    .SetParameter("xrefType", (int) CustomTagCrossRefType.Anime)
                    .List<CustomTag>();

            return new List<CustomTag>(tags);
        }

        public ILookup<int, CustomTag> GetByAnimeIDs(ISessionWrapper session, int[] animeIDs)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (animeIDs == null)
                throw new ArgumentNullException(nameof(animeIDs));

            if (animeIDs.Length == 0)
            {
                return EmptyLookup<int, CustomTag>.Instance;
            }

            var tags = session.CreateQuery(
                "Select xref.CrossRefID, tag FROM CustomTag as tag, CrossRef_CustomTag as xref WHERE tag.CustomTagID = xref.CustomTagID AND xref.CrossRefID IN (:animeIDs) AND xref.CrossRefType= :xrefType")
                .SetParameterList("animeIDs", animeIDs)
                .SetParameter("xrefType", (int)CustomTagCrossRefType.Anime)
                .List<object[]>()
                .ToLookup(t => (int)t[0], t => (CustomTag)t[1]);

            return tags;
        }

        public CustomTag GetByTagID(int id, ISession session)
        {
            CustomTag cr = session
                .CreateCriteria(typeof(CustomTag))
                .Add(Restrictions.Eq("CustomTagID", id))
                .UniqueResult<CustomTag>();

            return cr;
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