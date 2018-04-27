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

        /// <summary>
        /// Return a list of Anime IDs in a prequel/sequel line, including the given animeID, in order
        /// </summary>
        /// <param name="animeID"></param>
        /// <returns></returns>
        public List<int> GetFullLinearRelationTree(int animeID)
        {
            var allRelations = GetByAnimeID(animeID).Where(a => a?.RelationType == "Prequel").ToList();
            HashSet<int> visitedNodes = new HashSet<int>();
            List<AniDB_Anime_Relation> resultRelations = new List<AniDB_Anime_Relation>();
            GetAllRelationsByTypeRecursive(allRelations, ref visitedNodes, ref resultRelations, "Prequel");

            allRelations = GetByAnimeID(animeID).Where(a => a?.RelationType == "Sequel").ToList();
            visitedNodes.Clear();
            GetAllRelationsByTypeRecursive(allRelations, ref visitedNodes, ref resultRelations, "Sequel");

            return resultRelations.SelectMany(a => new[] {a.AnimeID, a.RelatedAnimeID}).Distinct().OrderBy(a => a)
                .ToList();
        }

        private void GetAllRelationsByTypeRecursive(List<AniDB_Anime_Relation> allRelations, ref HashSet<int> visitedNodes, ref List<AniDB_Anime_Relation> resultRelations, string type)
        {
            foreach (var relation in allRelations)
            {
                if (visitedNodes.Contains(relation.RelatedAnimeID)) continue;
                var sequels = GetByAnimeID(relation.RelatedAnimeID).Where(a => a?.RelationType == type).ToList();
                if (sequels.Count == 0) return;

                GetAllRelationsByTypeRecursive(sequels, ref visitedNodes, ref resultRelations, type);
                visitedNodes.Add(relation.RelatedAnimeID);
                resultRelations.AddRange(sequels);
            }
        }
    }
}
