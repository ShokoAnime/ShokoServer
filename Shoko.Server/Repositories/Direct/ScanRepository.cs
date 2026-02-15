using Shoko.Server.Databases;
using Shoko.Server.Models.Legacy;

namespace Shoko.Server.Repositories.Direct;

public class ScanRepository : BaseDirectRepository<Scan, int>
{
    public ScanRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
