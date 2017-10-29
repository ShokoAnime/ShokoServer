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
    public class CharacterRepository : BaseCachedRepository<Character, int>
    {
        private PocoIndex<int, Character, int> AniDBIDs;

        private CharacterRepository()
        {
        }

        public override void RegenerateDb()
        {
        }

        public static CharacterRepository Create()
        {
            return new CharacterRepository();
        }

        protected override int SelectKey(Character entity)
        {
            return entity.CharacterID;
        }

        public override void PopulateIndexes()
        {
            AniDBIDs = new PocoIndex<int, Character, int>(Cache, a => a.AniDBID);
        }


        public Character GetByAniDBID(int id)
        {
            lock (Cache)
            {
                return AniDBIDs.GetOne(id);
            }
        }
    }
}