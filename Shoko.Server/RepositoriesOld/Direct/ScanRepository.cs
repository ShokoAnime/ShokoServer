using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Direct
{
    public class ScanRepository : BaseDirectRepository<SVR_Scan, int>
    {
        private ScanRepository()
        {
        }

        public static ScanRepository Create()
        {
            return new ScanRepository();
        }
    }
}