using JMMServer.Entities;

namespace JMMServer.Repositories.Direct
{
    public class AniDB_MylistStatsRepository : BaseDirectRepository<AniDB_MylistStats, int>
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