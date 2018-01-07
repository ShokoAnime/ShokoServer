using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Repos
{
    public class AniDB_ReviewRepository : BaseRepository<AniDB_Review, int>
    {
        internal override int SelectKey(AniDB_Review entity) => entity.ReviewID;
            
        internal override void PopulateIndexes()
        {
        }

        internal override void ClearIndexes()
        {
        }
    }
}