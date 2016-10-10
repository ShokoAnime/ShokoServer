using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Collections;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class CustomTagRepository : BaseCachedRepository<CustomTag, int>
    {
        private CustomTagRepository()
        {
            DeleteWithOpenTransactionCallback = (ses, obj) =>
            {
                RepoFactory.CrossRef_CustomTag.DeleteWithOpenTransaction(ses, RepoFactory.CrossRef_CustomTag.GetByCustomTagID(obj.CustomTagID));
            };
        }

        public static CustomTagRepository Create()
        {
            return new CustomTagRepository();
        }

        protected override int SelectKey(CustomTag entity)
        {
            return entity.CustomTagID;
        }

        public override void PopulateIndexes()
        {
        }

        public override void RegenerateDb()
        {
        }

        public List<CustomTag> GetByAnimeID(int animeID)
        {
            return RepoFactory.CrossRef_CustomTag.GetByAnimeID(animeID)
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


         public Dictionary<int, List<CustomTag>> GetByAnimeIDs(ISessionWrapper session, int[] animeIDs)
         {
            return animeIDs.ToDictionary(a => a, a=> RepoFactory.CrossRef_CustomTag.GetByAnimeID(a).Select(b => GetByID(b.CustomTagID)).Where(b => b != null).ToList());
            /*
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

            return tags;*/
        }
    }
}