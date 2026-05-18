#nullable enable
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Direct.TMDB.Text;

public class TMDB_OverviewRepository : BaseDirectRepository<TMDB_Overview, int>
{
    public IReadOnlyList<TMDB_Overview> GetByParentTypeAndID(DataEntityType parentType, int parentId)
    {
        var foreignParentType = parentType.ForeignType;
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Overview>()
                .Where(a => a.ForeignParentType == foreignParentType && a.ParentID == parentId)
                .ToList();
        });
    }

    public TMDB_OverviewRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
