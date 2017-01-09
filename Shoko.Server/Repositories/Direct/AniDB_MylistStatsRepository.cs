using Shoko.Models.Server;
using Shoko.Server.Entities;

namespace Shoko.Server.Repositories.Direct
{
    public class AniDB_MylistStatsRepository : BaseDirectRepository<SVR_AniDB_MylistStats, int>
    {

        private AniDB_MylistStatsRepository()
        {
            
        }

        public static AniDB_MylistStatsRepository Create()
        {
            return new AniDB_MylistStatsRepository();
        }
 
    }
}