using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class AniDB_Anime_RelationRepository : BaseRepository<AniDB_Anime_Relation, int>
    {

        private PocoIndex<int, AniDB_Anime_Relation, int> Animes;
        private PocoIndex<int, AniDB_Anime_Relation, int, int> AnimeRelations;

        internal override int SelectKey(AniDB_Anime_Relation entity) => entity.AniDB_Anime_RelationID;

        internal override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, AniDB_Anime_Relation, int>(Cache, a => a.AnimeID);
            AnimeRelations = new PocoIndex<int, AniDB_Anime_Relation, int, int>(Cache, a => a.AnimeID, a=>a.RelatedAnimeID);
        }

        internal override void ClearIndexes()
        {
            Animes = null;
            AnimeRelations = null;
        }


        public AniDB_Anime_Relation GetByAnimeIDAndRelationID(int animeid, int relatedanimeid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return AnimeRelations.GetOne(animeid,relatedanimeid);
                return Table.FirstOrDefault(a => a.AnimeID == animeid && a.RelatedAnimeID == relatedanimeid);
            }
        }

        public List<AniDB_Anime_Relation> GetByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id);
                return Table.Where(a => a.AnimeID == id).ToList();
            }
        }
        public List<int> GetIdsByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id).Select(a=>a.AniDB_Anime_RelationID).ToList();
                return Table.Where(a => a.AnimeID == id).Select(a => a.AniDB_Anime_RelationID).ToList();
            }
        }

        /// SELECT AnimeID FROM AniDB_Anime_Relation WHERE (RelationType = 'Prequel' OR RelationType = 'Sequel') AND (AnimeID = 10445 OR RelatedAnimeID = 10445)
        /// UNION
        /// SELECT RelatedAnimeID AS AnimeID FROM AniDB_Anime_Relation WHERE (RelationType = 'Prequel' OR RelationType = 'Sequel') AND (AnimeID = 10445 OR RelatedAnimeID = 10445)
        public HashSet<int> GetLinearRelations(int id)
        {
            var cats = (from relation in Table
                        where (relation.AnimeID == id || relation.RelatedAnimeID == id) &&
                              (relation.RelationType == "Prequel" || relation.RelationType == "Sequel")
                        select relation.AnimeID);
            var cats2 = (from relation in Table
                         where (relation.AnimeID == id || relation.RelatedAnimeID == id) &&
                               (relation.RelationType == "Prequel" || relation.RelationType == "Sequel")
                         select relation.RelatedAnimeID);

            return new HashSet<int>(cats.Concat(cats2));
        }

        /// <summary>
        /// Return a list of Anime IDs in a prequel/sequel line, including the given animeID, in order
        /// </summary>
        /// <param name="animeID"></param>
        /// <returns></returns>
        public List<int> GetFullLinearRelationTree(int animeID)
        {
            using (RepoLock.ReaderLock())
            {
                var allRelations = GetLinearRelations(animeID);
                HashSet<int> visitedNodes = new HashSet<int> { animeID };
                HashSet<int> resultRelations = new HashSet<int>(allRelations);
                GetAllRelationsByTypeRecursive(allRelations, ref visitedNodes, ref resultRelations);

                return resultRelations.OrderBy(a => a).ToList();
            }
        }

        private void GetAllRelationsByTypeRecursive(IEnumerable<int> allRelations, ref HashSet<int> visitedNodes, ref HashSet<int> resultRelations)
        {
            foreach (var relation in allRelations)
            {
                if (visitedNodes.Contains(relation)) continue;
                var sequels = GetLinearRelations(relation);
                visitedNodes.Add(relation);
                if (sequels.Count == 0) continue;

                GetAllRelationsByTypeRecursive(sequels, ref visitedNodes, ref resultRelations);
                resultRelations.UnionWith(sequels);
            }
        }
    }
}