using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_Anime_RelationRepository : BaseDirectRepository<AniDB_Anime_Relation, int>
{
    public List<AniDB_Anime_Relation> GetByAnimeID(int id)
    {
        using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
        return GetByAnimeID(session.Wrap(), id);
    }

    public List<AniDB_Anime_Relation> GetByAnimeID(IEnumerable<int> ids)
    {
        var aids = ids.ToArray();
        using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
        return session.Query<AniDB_Anime_Relation>()
            .Where(a => aids.Contains(a.AnimeID))
            .ToList();
    }

    public List<AniDB_Anime_Relation> GetByAnimeID(ISessionWrapper session, int id)
    {
        return session.Query<AniDB_Anime_Relation>()
            .Where(a => a.AnimeID == id)
            .ToList();
    }

    public List<AniDB_Anime_Relation> GetByRelatedAnimeID(int id)
    {
        using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
        return GetByRelatedAnimeID(session.Wrap(), id);
    }

    public List<AniDB_Anime_Relation> GetByRelatedAnimeID(IEnumerable<int> ids)
    {
        var aids = ids.ToArray();
        using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
        return session.Query<AniDB_Anime_Relation>()
            .Where(a => aids.Contains(a.RelatedAnimeID))
            .ToList();
    }

    public List<AniDB_Anime_Relation> GetByRelatedAnimeID(ISessionWrapper session, int id)
    {
        return session.Query<AniDB_Anime_Relation>()
            .Where(a => a.RelatedAnimeID == id)
            .ToList();
    }

    public AniDB_Anime_RelationRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
