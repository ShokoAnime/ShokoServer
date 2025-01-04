#nullable enable
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Direct.TMDB.Text;

public class TMDB_TitleRepository : BaseDirectRepository<TMDB_Title, int>
{
    public IReadOnlyList<TMDB_Title> GetByParentTypeAndID(ForeignEntityType parentType, int parentId)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return session
                .Query<TMDB_Title>()
                .Where(a => a.ParentType == parentType && a.ParentID == parentId)
                .ToList();
        });
    }

    public TMDB_TitleRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
