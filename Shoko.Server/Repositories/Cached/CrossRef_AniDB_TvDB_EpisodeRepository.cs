using System.Collections.Generic;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Cached
{
    public class CrossRef_AniDB_TvDB_EpisodeRepository : BaseCachedRepository<CrossRef_AniDB_TvDB_Episode, int>
    {
        private PocoIndex<int, CrossRef_AniDB_TvDB_Episode, int> AnimeIDs;
        private PocoIndex<int, CrossRef_AniDB_TvDB_Episode, int> EpisodeIDs;

        public override void PopulateIndexes()
        {
            AnimeIDs = new PocoIndex<int, CrossRef_AniDB_TvDB_Episode, int>(Cache, a => a.AnimeID);
            EpisodeIDs = new PocoIndex<int, CrossRef_AniDB_TvDB_Episode, int>(Cache, a => a.AniDBEpisodeID);
        }

        private CrossRef_AniDB_TvDB_EpisodeRepository()
        {
        }

        public static CrossRef_AniDB_TvDB_EpisodeRepository Create()
        {
            return new CrossRef_AniDB_TvDB_EpisodeRepository();
        }

        public CrossRef_AniDB_TvDB_Episode GetByAniDBEpisodeID(int id)
        {
            // TODO Change this when multiple AniDB <=> TvDB Episode mappings
            // lock (Cache)
            {
                return EpisodeIDs.GetOne(id);
            }
        }

        public List<CrossRef_AniDB_TvDB_Episode> GetByAnimeID(int id)
        {
            // lock (Cache)
            {
                return AnimeIDs.GetMultiple(id);
            }
        }

        public override void RegenerateDb()
        {
        }

        protected override int SelectKey(CrossRef_AniDB_TvDB_Episode entity)
        {
            return entity.CrossRef_AniDB_TvDB_EpisodeID;
        }
    }
}