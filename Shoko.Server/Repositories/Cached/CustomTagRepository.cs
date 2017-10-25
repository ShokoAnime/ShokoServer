using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Models;
using Shoko.Models.Server;
using NHibernate;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories
{
    public class CustomTagRepository : BaseCachedRepository<CustomTag, int>
    {
        private CustomTagRepository()
        {
            DeleteWithOpenTransactionCallback = (ses, obj) =>
            {
                RepoFactory.CrossRef_CustomTag.DeleteWithOpenTransaction(ses,
                    RepoFactory.CrossRef_CustomTag.GetByCustomTagID(obj.CustomTagID));
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
        }


        public Dictionary<int, List<CustomTag>> GetByAnimeIDs(ISessionWrapper session, int[] animeIDs)
        {
            return animeIDs.ToDictionary(a => a,
                a => RepoFactory.CrossRef_CustomTag.GetByAnimeID(a)
                    .Select(b => GetByID(b.CustomTagID))
                    .Where(b => b != null)
                    .ToList());
        }
    }
}