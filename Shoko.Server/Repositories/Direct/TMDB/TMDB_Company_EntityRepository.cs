using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Repositories.Direct.TMDB;

public class TMDB_Company_EntityRepository : BaseDirectRepository<TMDB_Company_Entity, int>
{
    public IReadOnlyList<TMDB_Company_Entity> GetByTmdbCompanyID(int companyId)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_Company_Entity>()
            .Where(a => a.TmdbCompanyID == companyId)
            .OrderBy(xref => xref.ReleasedAt ?? DateOnly.MaxValue)
            .ToList();
    }

    public IReadOnlyList<TMDB_Company_Entity> GetByTmdbEntityTypeAndCompanyID(DataEntityType entityType, int companyId)
    {
        var foreignEntityType = entityType.ForeignType;
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_Company_Entity>()
            .Where(a => a.TmdbCompanyID == companyId && a.ForeignTmdbEntityType == foreignEntityType)
            .OrderBy(xref => xref.ReleasedAt ?? DateOnly.MaxValue)
            .ToList();
    }

    public IReadOnlyList<TMDB_Company_Entity> GetByTmdbEntityTypeAndID(DataEntityType entityType, int entityId)
    {
        var foreignEntityType = entityType.ForeignType;
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_Company_Entity>()
            .Where(a => a.ForeignTmdbEntityType == foreignEntityType && a.TmdbEntityID == entityId)
            .OrderBy(xref => xref.Ordering)
            .ToList();
    }

    public TMDB_Company_EntityRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
