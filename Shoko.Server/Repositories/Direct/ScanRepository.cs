using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Direct;

public class ScanRepository : BaseDirectRepository<SVR_Scan, int>
{
    public ScanRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
