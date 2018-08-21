using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.Cached;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Server.Models;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class AnimeCharacterRepository : BaseRepository<AnimeCharacter, int>
    {
        private PocoIndex<int, AnimeCharacter, int> AniDBIDs;

        internal override int SelectKey(AnimeCharacter entity) => entity.CharacterID;

        internal override void PopulateIndexes()
        {
            AniDBIDs = new PocoIndex<int, AnimeCharacter, int>(Cache, a => a.AniDBID);
        }

        internal override void ClearIndexes()
        {
            AniDBIDs = null;
        }

        internal AnimeCharacter GetByAniDBID(int charID)
        {
            using (RepoLock.ReaderLock())
            {
                return Where(s => s.AniDBID == charID).FirstOrDefault();
            }
        }
    }
}
