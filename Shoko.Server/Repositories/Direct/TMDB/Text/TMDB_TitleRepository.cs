using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Direct.TMDB.Text;

public class TMDB_TitleRepository : BaseDirectRepository<TMDB_Title, int>
{
    public IReadOnlyList<TMDB_Title> GetByParentTypeAndID(DataEntityType parentType, int parentId)
    {
        var foreignParentType = parentType.ForeignType;
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_Title>()
            .Where(a => a.ForeignParentType == foreignParentType && a.ParentID == parentId)
            .ToList();
    }

    public TMDB_TitleRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
