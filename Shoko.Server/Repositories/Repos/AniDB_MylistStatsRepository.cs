using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Repos
{
    public class AniDB_MylistStatsRepository : BaseRepository<AniDB_MylistStats, int>
    {
        internal override int SelectKey(AniDB_MylistStats entity) => entity.AniDB_MylistStatsID;
            
        internal override void PopulateIndexes()
        {
        }

        internal override void ClearIndexes()
        {
        }

    }
}