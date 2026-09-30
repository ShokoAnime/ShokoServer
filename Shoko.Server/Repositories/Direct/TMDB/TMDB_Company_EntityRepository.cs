using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Direct.TMDB;

public class TMDB_Company_EntityRepository(DatabaseFactory databaseFactory) : BaseDirectRepository<TMDB_Company_Entity, int>(databaseFactory)
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

    public IReadOnlyList<TMDB_Company_Entity> GetByTmdbEntityTypeAndCompanyID(MetadataEntityType entityType, int companyId)
    {
        var foreignEntityType = entityType.ForeignType;
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_Company_Entity>()
            .Where(a => a.TmdbCompanyID == companyId && a.ForeignTmdbEntityType == foreignEntityType)
            .OrderBy(xref => xref.ReleasedAt ?? DateOnly.MaxValue)
            .ToList();
    }

    public IReadOnlyList<TMDB_Company_Entity> GetByTmdbEntityTypeAndID(MetadataEntityType entityType, int entityId)
    {
        var foreignEntityType = entityType.ForeignType;
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_Company_Entity>()
            .Where(a => a.ForeignTmdbEntityType == foreignEntityType && a.TmdbEntityID == entityId)
            .OrderBy(xref => xref.Ordering)
            .ToList();
    }

    /// <summary>
    ///   Every ID of one kind of entity that a company is credited on.
    /// </summary>
    /// <param name="entityType">The kind of entity.</param>
    /// <returns>The IDs, each once.</returns>
    public IReadOnlyList<int> GetAllTmdbEntityIDs(MetadataEntityType entityType)
    {
        var foreignEntityType = entityType.ForeignType;
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_Company_Entity>()
            .Where(a => a.ForeignTmdbEntityType == foreignEntityType)
            .Select(a => a.TmdbEntityID)
            .Distinct()
            .ToList();
    }
}
