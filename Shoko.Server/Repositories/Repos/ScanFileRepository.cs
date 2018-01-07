using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Enums;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Repos
{
    public class ScanFileRepository : BaseRepository<ScanFile, int>
    {
        private PocoIndex<int, ScanFile, int> Scans;
        private PocoIndex<int, ScanFile, int, int> ScanStatus;

        internal override int SelectKey(ScanFile entity) => entity.ScanFileID;

        internal override void PopulateIndexes()
        {
            Scans = new PocoIndex<int, ScanFile, int>(Cache, a => a.ScanID);
            ScanStatus = new PocoIndex<int, ScanFile, int, int>(Cache, a => a.ScanID,a=>a.Status);
        }

        internal override void ClearIndexes()
        {
            Scans = null;
            ScanStatus = null;
        }

        public List<ScanFile> GetByScanID(int scanid)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Scans.GetMultiple(scanid).OrderBy(a=>a.CheckDate).ToList();
                return Table.Where(a=>a.ScanID==scanid).OrderBy(a => a.CheckDate).ToList();
            }
        }
        public List<ScanFile> GetWaiting(int scanid)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return ScanStatus.GetMultiple(scanid, (int)ScanFileStatus.Waiting).OrderBy(a=>a.CheckDate).ToList();
                return Table.Where(a=>a.ScanID==scanid && a.Status== (int)ScanFileStatus.Waiting).OrderBy(a => a.CheckDate).ToList();
            }
        }



        public List<ScanFile> GetProcessedOK(int scanid)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return ScanStatus.GetMultiple(scanid, (int)ScanFileStatus.ProcessedOK).OrderBy(a => a.CheckDate).ToList();
                return Table.Where(a => a.ScanID == scanid && a.Status == (int)ScanFileStatus.ProcessedOK).OrderBy(a => a.CheckDate).ToList();
            }
        }

        public List<ScanFile> GetWithError(int scanid)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Scans.GetMultiple(scanid).Where(a=>a.Status > (int)ScanFileStatus.ProcessedOK).OrderBy(a => a.CheckDate).ToList();
                return Table.Where(a => a.ScanID == scanid && a.Status > (int)ScanFileStatus.ProcessedOK).OrderBy(a => a.CheckDate).ToList();
            }
        }

        public int GetWaitingCount(int scanid)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return ScanStatus.GetMultiple(scanid, (int) ScanFileStatus.Waiting).Count;
                return Table.Count(a => a.ScanID == scanid && a.Status == (int) ScanFileStatus.Waiting);
            }
        }
    }
}