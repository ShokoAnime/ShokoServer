using System;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class AniDB_ReleaseGroupRepository : BaseRepository<AniDB_ReleaseGroup, int>
    {
        internal override int SelectKey(AniDB_ReleaseGroup entity) => entity.GroupID;

        internal override void PopulateIndexes()
        {
        }

        internal override void ClearIndexes()
        {
        }

        internal AniDB_ReleaseGroup GetByGroupID(int groupID)
        {
            using (RepoLock.ReaderLock())
            {
                return Table.FirstOrDefault(s => s.GroupID == groupID);
            }
        }
    }
}