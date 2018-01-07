using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Repos
{
    public class ScanRepository : BaseRepository<SVR_Scan, int>
    {

        internal override int SelectKey(SVR_Scan entity) => entity.ScanID;
            
        internal override void PopulateIndexes()
        {
        }

        internal override void ClearIndexes()
        {
        }
        public static ScanRepository Create()
        {
            return new ScanRepository();
        }
    }
}