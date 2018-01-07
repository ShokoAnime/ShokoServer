using Shoko.Models.Server;

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
    }
}