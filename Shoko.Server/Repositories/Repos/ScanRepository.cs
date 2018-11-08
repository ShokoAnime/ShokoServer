using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Repos
{
    public class ScanRepository : BaseRepository<Scan, int>
    {

        internal override int SelectKey(Scan entity) => entity.ScanID;
            
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