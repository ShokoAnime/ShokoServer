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
    public class StaffRepository : BaseCachedRepository<Staff, int>
    {
        private PocoIndex<int, Staff, int> AniDBIDs;

        private StaffRepository()
        {
        }

        public override void RegenerateDb()
        {
        }

        public static StaffRepository Create()
        {
            return new StaffRepository();
        }

        protected override int SelectKey(Staff entity)
        {
            return entity.StaffID;
        }

        public override void PopulateIndexes()
        {
            AniDBIDs = new PocoIndex<int, Staff, int>(Cache, a => a.AniDBID);
        }


        public Staff GetByAniDBID(int id)
        {
            lock (Cache)
            {
                return AniDBIDs.GetOne(id);
            }
        }
    }
}