using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
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
