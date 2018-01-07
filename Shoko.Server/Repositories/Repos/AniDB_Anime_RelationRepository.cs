using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

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
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return AnimeRelations.GetOne(animeid,relatedanimeid);
                return Table.FirstOrDefault(a => a.AnimeID == animeid && a.RelatedAnimeID == relatedanimeid);
            }
        }

        public List<AniDB_Anime_Relation> GetByAnimeID(int id)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id);
                return Table.Where(a => a.AnimeID == id).ToList();
            }
        }
        public List<int> GetIdsByAnimeID(int id)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id).Select(a=>a.AniDB_Anime_RelationID).ToList();
                return Table.Where(a => a.AnimeID == id).Select(a => a.AniDB_Anime_RelationID).ToList();
            }
        }
    }
}