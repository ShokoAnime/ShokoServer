using System.Collections.Generic;
using System.Linq;
using JMMServer.Entities;
using NHibernate.Criterion;

namespace JMMServer.Repositories.Direct
{
    public class ScanFileRepository : BaseDirectRepository<ScanFile, int>
    {
        private ScanFileRepository()
        {
            
        }

        public static ScanFileRepository Create()
        {
            return new ScanFileRepository();
        }

        public List<ScanFile> GetWaiting(int scanid)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.CreateCriteria(typeof(ScanFile))
                .Add(Restrictions.Eq("ScanID", scanid))
                .Add(Restrictions.Eq("Status", (int)ScanFileStatus.Waiting)).AddOrder(Order.Asc("CheckDate"))
                .List<ScanFile>().ToList();
            }
        }
        public List<ScanFile> GetByScanID(int scanid)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.CreateCriteria(typeof(ScanFile))
                .Add(Restrictions.Eq("ScanID", scanid))
                .List<ScanFile>().ToList();
            }
        }
        public List<ScanFile> GetProcessedOK(int scanid)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.CreateCriteria(typeof(ScanFile))
                .Add(Restrictions.Eq("ScanID", scanid))
                .Add(Restrictions.Eq("Status", (int)ScanFileStatus.ProcessedOK)).AddOrder(Order.Asc("CheckDate"))
                .List<ScanFile>().ToList();
            }
        }
        public List<ScanFile> GetWithError(int scanid)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.CreateCriteria(typeof(ScanFile))
                .Add(Restrictions.Eq("ScanID", scanid))
                .Add(Restrictions.Gt("Status", (int)ScanFileStatus.ProcessedOK)).AddOrder(Order.Asc("CheckDate"))
                .List<ScanFile>().ToList();
            }
        }

        public int GetWaitingCount(int scanid)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return (int)session.CreateCriteria(typeof(ScanFile))
                .Add(Restrictions.Eq("ScanID", scanid))
                .Add(Restrictions.Eq("Status", (int)ScanFileStatus.Waiting))
                .SetProjection(Projections.Count("ScanFileID")).UniqueResult();
            }
        }
    }
}
