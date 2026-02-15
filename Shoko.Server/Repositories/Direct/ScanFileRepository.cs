using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.Legacy;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Direct;

public class ScanFileRepository(DatabaseFactory databaseFactory) : BaseDirectRepository<ScanFile, int>(databaseFactory)
{
    public List<ScanFile> GetWaiting(int scanID)
        => Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<ScanFile>()
                .Where(a => a.ScanID == scanID && a.Status == ScanFileStatus.Waiting)
                .OrderBy(a => a.CheckDate)
                .ToList();
        });

    public List<ScanFile> GetByScanID(int scanID)
        => Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<ScanFile>()
                .Where(a => a.ScanID == scanID)
                .ToList();
        });

    public List<ScanFile> GetWithError(int scanID)
        => Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<ScanFile>()
                .Where(a => a.ScanID == scanID && a.Status > ScanFileStatus.ProcessedOK)
                .OrderBy(a => a.CheckDate)
                .ToList();
        });

    public int GetWaitingCount(int scanID)
        => Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session.Query<ScanFile>()
                .Count(a => a.ScanID == scanID && a.Status == (int)ScanFileStatus.Waiting);
        });
}
