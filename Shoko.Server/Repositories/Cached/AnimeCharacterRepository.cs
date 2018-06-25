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
    public class AnimeCharacterRepository : BaseCachedRepository<AnimeCharacter, int>
    {
        private PocoIndex<int, AnimeCharacter, int> AniDBIDs;

        private AnimeCharacterRepository()
        {
        }

        public override void RegenerateDb()
        {
        }

        public static AnimeCharacterRepository Create()
        {
            var repo = new AnimeCharacterRepository();
            Repo.CachedRepositories.Add(repo);
            return repo;
        }

        protected override int SelectKey(AnimeCharacter entity)
        {
            return entity.CharacterID;
        }

        public override void PopulateIndexes()
        {
            AniDBIDs = new PocoIndex<int, AnimeCharacter, int>(Cache, a => a.AniDBID);
        }


        public AnimeCharacter GetByAniDBID(int id)
        {
            lock (Cache)
            {
                return AniDBIDs.GetOne(id);
            }
        }
    }
}
