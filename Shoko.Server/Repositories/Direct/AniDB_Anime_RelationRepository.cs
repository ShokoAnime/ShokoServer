using System.Collections.Generic;
using System.Linq;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct;

public class AniDB_Anime_RelationRepository : BaseDirectRepository<SVR_AniDB_Anime_Relation, int>
{
    public SVR_AniDB_Anime_Relation GetByAnimeIDAndRelationID(int animeid, int relatedanimeid)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cr = session
                .CreateCriteria(typeof(SVR_AniDB_Anime_Relation))
                .Add(Restrictions.Eq("AnimeID", animeid))
                .Add(Restrictions.Eq("RelatedAnimeID", relatedanimeid))
                .UniqueResult<SVR_AniDB_Anime_Relation>();
            return cr;
        }
    }

    public List<SVR_AniDB_Anime_Relation> GetByAnimeID(int id)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            return GetByAnimeID(session.Wrap(), id);
        }
    }

    public List<SVR_AniDB_Anime_Relation> GetByAnimeID(IEnumerable<int> ids)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var cats = session
                .CreateCriteria(typeof(SVR_AniDB_Anime_Relation))
                .Add(Restrictions.In("AnimeID", ids.ToArray()))
                .List<SVR_AniDB_Anime_Relation>();

            return new List<SVR_AniDB_Anime_Relation>(cats);
        }
    }

    public List<SVR_AniDB_Anime_Relation> GetByAnimeID(ISessionWrapper session, int id)
    {
        lock (GlobalDBLock)
        {
            var cats = session
                .CreateCriteria(typeof(SVR_AniDB_Anime_Relation))
                .Add(Restrictions.Eq("AnimeID", id))
                .List<SVR_AniDB_Anime_Relation>();

            return new List<SVR_AniDB_Anime_Relation>(cats);
        }
    }

    /// SELECT AnimeID FROM AniDB_Anime_Relation WHERE (RelationType = 'Prequel' OR RelationType = 'Sequel') AND (AnimeID = 10445 OR RelatedAnimeID = 10445)
    /// UNION
    /// SELECT RelatedAnimeID AS AnimeID FROM AniDB_Anime_Relation WHERE (RelationType = 'Prequel' OR RelationType = 'Sequel') AND (AnimeID = 10445 OR RelatedAnimeID = 10445)
    private HashSet<int> GetLinearRelations(ISession session, int id)
    {
        lock (GlobalDBLock)
        {
            var cats = (from relation in session.QueryOver<SVR_AniDB_Anime_Relation>()
                where (relation.AnimeID == id || relation.RelatedAnimeID == id) &&
                      (relation.RelationType == "Prequel" || relation.RelationType == "Sequel")
                select relation.AnimeID).List<int>();
            var cats2 = (from relation in session.QueryOver<SVR_AniDB_Anime_Relation>()
                where (relation.AnimeID == id || relation.RelatedAnimeID == id) &&
                      (relation.RelationType == "Prequel" || relation.RelationType == "Sequel")
                select relation.RelatedAnimeID).List<int>();
            return new HashSet<int>(cats.Concat(cats2));
        }
    }

    /// <summary>
    /// Return a list of Anime IDs in a prequel/sequel line, including the given animeID, in order
    /// </summary>
    /// <param name="animeID"></param>
    /// <returns></returns>
    public List<int> GetFullLinearRelationTree(int animeID)
    {
        lock (GlobalDBLock)
        {
            using var session = DatabaseFactory.SessionFactory.OpenSession();
            var allRelations = GetLinearRelations(session, animeID);
            var visitedNodes = new HashSet<int> { animeID };
            var resultRelations = new HashSet<int>(allRelations);
            GetAllRelationsByTypeRecursive(session, allRelations, ref visitedNodes, ref resultRelations);

            return resultRelations.OrderBy(a => a).ToList();
        }
    }

    private void GetAllRelationsByTypeRecursive(ISession session, IEnumerable<int> allRelations,
        ref HashSet<int> visitedNodes, ref HashSet<int> resultRelations)
    {
        foreach (var relation in allRelations)
        {
            if (visitedNodes.Contains(relation))
            {
                continue;
            }

            var sequels = GetLinearRelations(session, relation);
            visitedNodes.Add(relation);
            if (sequels.Count == 0)
            {
                continue;
            }

            GetAllRelationsByTypeRecursive(session, sequels, ref visitedNodes, ref resultRelations);
            resultRelations.UnionWith(sequels);
        }
    }
}
