using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_Anime_RelationRepository : BaseDirectRepository<AniDB_Anime_Relation, int>
{
    public AniDB_Anime_Relation GetByAnimeIDAndRelationID(int animeid, int relatedanimeid)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
            var cr = session
                .Query<AniDB_Anime_Relation>()
                .FirstOrDefault(a => a.AnimeID == animeid && a.RelatedAnimeID == relatedanimeid);
            return cr;
        });
    }

    public List<AniDB_Anime_Relation> GetByAnimeID(int id)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
            return GetByAnimeID(session.Wrap(), id);
        });
    }

    public List<AniDB_Anime_Relation> GetByAnimeID(IEnumerable<int> ids)
    {
        var aids = ids.ToArray();
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
            return session.Query<AniDB_Anime_Relation>()
                .Where(a => aids.Contains(a.AnimeID))
                .ToList();
        });
    }

    public List<AniDB_Anime_Relation> GetByAnimeID(ISessionWrapper session, int id)
    {
        return Lock(() => session.Query<AniDB_Anime_Relation>()
            .Where(a => a.AnimeID == id)
            .ToList());
    }

    public List<AniDB_Anime_Relation> GetByRelatedAnimeID(int id)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
            return GetByRelatedAnimeID(session.Wrap(), id);
        });
    }

    public List<AniDB_Anime_Relation> GetByRelatedAnimeID(IEnumerable<int> ids)
    {
        var aids = ids.ToArray();
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
            return session.Query<AniDB_Anime_Relation>()
                .Where(a => aids.Contains(a.RelatedAnimeID))
                .ToList();
        });
    }

    public List<AniDB_Anime_Relation> GetByRelatedAnimeID(ISessionWrapper session, int id)
    {
        return Lock(() => session.Query<AniDB_Anime_Relation>()
            .Where(a => a.RelatedAnimeID == id)
            .ToList());
    }

    /// <summary>
    /// Return a list of Anime IDs in a prequel/sequel line, including the given animeID, in order
    /// </summary>
    /// <param name="animeID"></param>
    /// <returns></returns>
    public List<int> GetFullLinearRelationTree(int animeID)
    {
        return Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
            var resultRelations = GetAllLinearRelations(session.Wrap(), animeID);
            return resultRelations.OrderBy(a => a).ToList();
        });
    }

    private HashSet<int> GetAllLinearRelations(ISessionWrapper session, int animeID)
    {
        var allRelations = new Queue<int>();
        var visitedNodes = new HashSet<int>();
        var resultRelations = new HashSet<int>();

        // add the first node
        allRelations.Enqueue(animeID);

        // loop the queue
        while (true)
        {
            // get and remove first entry; break when empty
            if (!allRelations.TryDequeue(out var relation)) break;
            // skip if we've already done it
            if (!visitedNodes.Add(relation)) continue;

            // actually get the relations
            var sequels = GetLinearRelationsUnsafe(session, relation);
            if (sequels.Count == 0) continue;

            // add the new nodes to the queue
            foreach (var sequel in sequels) allRelations.Enqueue(sequel);
            // add the new nodes to the results
            resultRelations.UnionWith(sequels);
        }

        return resultRelations;
    }

    private static HashSet<int> GetLinearRelationsUnsafe(ISessionWrapper session, int id)
    {
        var cats = session.Query<AniDB_Anime_Relation>()
            .Where(relation => (relation.AnimeID == id || relation.RelatedAnimeID == id) &&
                               (relation.RelationType == "Prequel" || relation.RelationType == "Sequel"))
            .Select(relation => relation.AnimeID).ToList();
        var cats2 = session.Query<AniDB_Anime_Relation>()
            .Where(relation => (relation.AnimeID == id || relation.RelatedAnimeID == id) &&
                               (relation.RelationType == "Prequel" || relation.RelationType == "Sequel"))
            .Select(relation => relation.RelatedAnimeID).ToList();
        return new HashSet<int>(cats.Concat(cats2));
    }

    public AniDB_Anime_RelationRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
