using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using NHibernate;
using NHibernate.Criterion;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Direct
{
    public class AniDB_Anime_RelationRepository : BaseDirectRepository<AniDB_Anime_Relation, int>
    {
        private AniDB_Anime_RelationRepository()
        {
        }

        public static AniDB_Anime_RelationRepository Create()
        {
            return new AniDB_Anime_RelationRepository();
        }

        public AniDB_Anime_Relation GetByAnimeIDAndRelationID(int animeid, int relatedanimeid)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByAnimeIDAndRelationID(session, animeid, relatedanimeid);
            }
        }

        public AniDB_Anime_Relation GetByAnimeIDAndRelationID(ISession session, int animeid, int relatedanimeid)
        {
            AniDB_Anime_Relation cr = session
                .CreateCriteria(typeof(AniDB_Anime_Relation))
                .Add(Restrictions.Eq("AnimeID", animeid))
                .Add(Restrictions.Eq("RelatedAnimeID", relatedanimeid))
                .UniqueResult<AniDB_Anime_Relation>();
            return cr;
        }

        public List<AniDB_Anime_Relation> GetByAnimeID(int id)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                return GetByAnimeID(session.Wrap(), id);
            }
        }

        public List<AniDB_Anime_Relation> GetByAnimeID(ISessionWrapper session, int id)
        {
            var cats = session
                .CreateCriteria(typeof(AniDB_Anime_Relation))
                .Add(Restrictions.Eq("AnimeID", id))
                .List<AniDB_Anime_Relation>();

            return new List<AniDB_Anime_Relation>(cats);
        }

        /// SELECT AnimeID FROM AniDB_Anime_Relation WHERE (RelationType = 'Prequel' OR RelationType = 'Sequel') AND (AnimeID = 10445 OR RelatedAnimeID = 10445)
        /// UNION
        /// SELECT RelatedAnimeID AS AnimeID FROM AniDB_Anime_Relation WHERE (RelationType = 'Prequel' OR RelationType = 'Sequel') AND (AnimeID = 10445 OR RelatedAnimeID = 10445)
        public HashSet<int> GetLinearRelations(ISession session, int id)
        {
            var cats = (from relation in session.QueryOver<AniDB_Anime_Relation>()
                where (relation.AnimeID == id || relation.RelatedAnimeID == id) &&
                      (relation.RelationType == "Prequel" || relation.RelationType == "Sequel")
                select relation.AnimeID).Future<int>();
            var cats2 = (from relation in session.QueryOver<AniDB_Anime_Relation>()
                where (relation.AnimeID == id || relation.RelatedAnimeID == id) &&
                      (relation.RelationType == "Prequel" || relation.RelationType == "Sequel")
                select relation.RelatedAnimeID).Future<int>();
            return new HashSet<int>(cats.Concat(cats2));
        }

        /// <summary>
        /// Return a list of Anime IDs in a prequel/sequel line, including the given animeID, in order
        /// </summary>
        /// <param name="animeID"></param>
        /// <returns></returns>
        public List<int> GetFullLinearRelationTree(int animeID)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                var allRelations = GetLinearRelations(session, animeID);
                HashSet<int> visitedNodes = new HashSet<int> { animeID };
                HashSet<int> resultRelations = new HashSet<int>(allRelations);
                GetAllRelationsByTypeRecursive(session, allRelations, ref visitedNodes, ref resultRelations);

                return resultRelations.OrderBy(a => a).ToList();
            }
        }

        private void GetAllRelationsByTypeRecursive(ISession session, IEnumerable<int> allRelations, ref HashSet<int> visitedNodes, ref HashSet<int> resultRelations)
        {
            foreach (var relation in allRelations)
            {
                if (visitedNodes.Contains(relation)) continue;
                var sequels = GetLinearRelations(session, relation);
                visitedNodes.Add(relation);
                if (sequels.Count == 0) continue;

                GetAllRelationsByTypeRecursive(session, sequels, ref visitedNodes, ref resultRelations);
                resultRelations.UnionWith(sequels);
            }
        }
    }
}
