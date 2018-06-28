using System;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class AniDB_SeiyuuRepository : BaseRepository<AniDB_Seiyuu, int>
    {
        internal override int SelectKey(AniDB_Seiyuu entity) => entity.SeiyuuID;
        
        internal override void PopulateIndexes()
        {
        }

        internal override void ClearIndexes()
        {
        }

        internal AniDB_Seiyuu GetBySeiyuuID(int entityID)
        {
            using (RepoLock.ReaderLock())
            {

            }
        }
    }
}