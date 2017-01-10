using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using NHibernate.Criterion;
using Shoko.Models;
using Shoko.Server.Databases;
using Shoko.Server.Entities;

namespace Shoko.Server.Repositories.Direct
{
    public class ScanFileRepository : BaseDirectRepository<SVR_ScanFile, int>
    {
        private ScanFileRepository()
        {
            
        }

        public static ScanFileRepository Create()
        {
            return new ScanFileRepository();
        }

        public List<SVR_ScanFile> GetWaiting(int scanid)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return session.CreateCriteria(typeof(SVR_ScanFile))
                .Add(Restrictions.Eq("ScanID", scanid))
                .Add(Restrictions.Eq("Status", (int)ScanFileStatus.Waiting)).AddOrder(Order.Asc("CheckDate"))
                .List<SVR_ScanFile>().ToList();
            }
        }
        public List<SVR_ScanFile> GetByScanID(int scanid)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return session.CreateCriteria(typeof(SVR_ScanFile))
                .Add(Restrictions.Eq("ScanID", scanid))
                .List<SVR_ScanFile>().ToList();
            }
        }
        public List<SVR_ScanFile> GetProcessedOK(int scanid)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return session.CreateCriteria(typeof(SVR_ScanFile))
                .Add(Restrictions.Eq("ScanID", scanid))
                .Add(Restrictions.Eq("Status", (int)ScanFileStatus.ProcessedOK)).AddOrder(Order.Asc("CheckDate"))
                .List<SVR_ScanFile>().ToList();
            }
        }
        public List<SVR_ScanFile> GetWithError(int scanid)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return session.CreateCriteria(typeof(SVR_ScanFile))
                .Add(Restrictions.Eq("ScanID", scanid))
                .Add(Restrictions.Gt("Status", (int)ScanFileStatus.ProcessedOK)).AddOrder(Order.Asc("CheckDate"))
                .List<SVR_ScanFile>().ToList();
            }
        }

        public int GetWaitingCount(int scanid)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return (int)session.CreateCriteria(typeof(SVR_ScanFile))
                .Add(Restrictions.Eq("ScanID", scanid))
                .Add(Restrictions.Eq("Status", (int)ScanFileStatus.Waiting))
                .SetProjection(Projections.Count("ScanFileID")).UniqueResult();
            }
        }
    }
}
