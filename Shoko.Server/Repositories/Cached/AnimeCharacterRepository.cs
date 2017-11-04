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
            var list = Cache.Values
                .Where(character => character.Description != null && character.Description.Contains("`")).ToList();
            int i = 0;
            foreach (var character in list)
            {
                character.Description = character.Description.Replace("`", "'");
                Save(character);
                i++;
                if (i % 10 == 0)
                    ServerState.Instance.CurrentSetupStatus = string.Format(
                        Commons.Properties.Resources.Database_Validating, typeof(AnimeCharacter).Name,
                        $" DbRegen - {i}/{list.Count}");
            }
        }

        public static AnimeCharacterRepository Create()
        {
            return new AnimeCharacterRepository();
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