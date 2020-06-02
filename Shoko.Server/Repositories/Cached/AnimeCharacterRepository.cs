using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories
{
    public class AnimeCharacterRepository : BaseCachedRepository<AnimeCharacter, int>
    {
        private PocoIndex<int, AnimeCharacter, int> AniDBIDs;

        public override void RegenerateDb()
        {
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
