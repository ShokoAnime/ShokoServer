using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.Cached;
using NHibernate;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories
{
    public class AnimeStaffRepository : BaseCachedRepository<AnimeStaff, int>
    {
        private PocoIndex<int, AnimeStaff, int> AniDBIDs;

        private AnimeStaffRepository()
        {
        }

        public override void RegenerateDb()
        {
        }

        public static AnimeStaffRepository Create()
        {
            return new AnimeStaffRepository();
        }

        protected override int SelectKey(AnimeStaff entity)
        {
            return entity.StaffID;
        }

        public override void PopulateIndexes()
        {
            AniDBIDs = new PocoIndex<int, AnimeStaff, int>(Cache, a => a.AniDBID);
        }


        public AnimeStaff GetByAniDBID(int id)
        {
            // lock (Cache)
            {
                return AniDBIDs.GetOne(id);
            }
        }
    }
}