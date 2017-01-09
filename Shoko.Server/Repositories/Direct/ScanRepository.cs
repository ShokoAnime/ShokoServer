using Shoko.Models.Server;
using NHibernate.Criterion;
using Shoko.Server.Entities;

namespace Shoko.Server.Repositories.Direct
{
    public class ScanRepository : BaseDirectRepository<Scan, int>
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
