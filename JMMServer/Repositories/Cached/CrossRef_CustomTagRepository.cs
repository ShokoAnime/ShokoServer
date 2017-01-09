using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Entities;
using Shoko.Models.Server;
using NHibernate;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;
using Shoko.Models;

namespace JMMServer.Repositories
{
    public class CrossRef_CustomTagRepository : BaseCachedRepository<CrossRef_CustomTag, int>
    {
        private PocoIndex<int, CrossRef_CustomTag, int> Tags;
        private PocoIndex<int, CrossRef_CustomTag, int, int> Refs;
        private CrossRef_CustomTagRepository()
        {
        }

        protected override int SelectKey(CrossRef_CustomTag entity)
        {
            return entity.CrossRef_CustomTagID;
        }

        public override void PopulateIndexes()
        {
            Tags = new PocoIndex<int, CrossRef_CustomTag, int>(Cache, a => a.CustomTagID);
            Refs = new PocoIndex<int, CrossRef_CustomTag, int, int>(Cache, a => a.CrossRefID, a => a.CrossRefType);
        }

        public override void RegenerateDb() { }


        public static CrossRef_CustomTagRepository Create()
        {
            return new CrossRef_CustomTagRepository();
        }
        public List<CrossRef_CustomTag> GetByAnimeID(int id)
        {
            return Refs.GetMultiple(id, (int) CustomTagCrossRefType.Anime);

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

    }
}