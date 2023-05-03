using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct;

public class ScanFileRepository : BaseDirectRepository<ScanFile, int>
{
    public List<ScanFile> GetWaiting(int scanid)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.Query<ScanFile>()
                .Where(a => a.ScanID == scanid && a.Status == (int)ScanFileStatus.Waiting)
                .OrderBy(a => a.CheckDate)
                .ToList();
        });
    }

    public List<ScanFile> GetByScanID(int scanid)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.Query<ScanFile>()
                .Where(a => a.ScanID == scanid)
                .ToList();
        });
    }

    public List<ScanFile> GetWithError(int scanid)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.Query<ScanFile>()
                .Where(a => a.ScanID == scanid && a.Status > (int)ScanFileStatus.ProcessedOK)
                .OrderBy(a => a.CheckDate)
                .ToList();
        });
    }

    public int GetWaitingCount(int scanid)
    {
        return Lock(() =>
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return session.Query<ScanFile>()
                .Count(a => a.ScanID == scanid && a.Status == (int)ScanFileStatus.Waiting);
        });
    }
}
