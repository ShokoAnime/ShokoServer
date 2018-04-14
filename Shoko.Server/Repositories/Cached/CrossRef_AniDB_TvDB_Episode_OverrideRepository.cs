using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Cached
{
    public class CrossRef_AniDB_TvDB_Episode_OverrideRepository : BaseCachedRepository<CrossRef_AniDB_TvDB_Episode_Override, int>
    {
        private PocoIndex<int, CrossRef_AniDB_TvDB_Episode_Override, int> AnimeIDs;
        private PocoIndex<int, CrossRef_AniDB_TvDB_Episode_Override, int> EpisodeIDs;

        public override void PopulateIndexes()
        {
            AnimeIDs = new PocoIndex<int, CrossRef_AniDB_TvDB_Episode_Override, int>(Cache,
                a => RepoFactory.AniDB_Episode.GetByEpisodeID(a.AniDBEpisodeID)?.AnimeID ?? -1);
            EpisodeIDs = new PocoIndex<int, CrossRef_AniDB_TvDB_Episode_Override, int>(Cache, a => a.AniDBEpisodeID);
        }

        private CrossRef_AniDB_TvDB_Episode_OverrideRepository()
        {
        }

        public static CrossRef_AniDB_TvDB_Episode_OverrideRepository Create()
        {
            var repo = new CrossRef_AniDB_TvDB_Episode_OverrideRepository();
            RepoFactory.CachedRepositories.Add(repo);
            return repo;
        }

        public CrossRef_AniDB_TvDB_Episode_Override GetByAniDBAndTvDBEpisodeIDs(int anidbID, int tvdbID)
        {
            lock (Cache)
            {
                return EpisodeIDs.GetMultiple(anidbID).FirstOrDefault(a => a.TvDBEpisodeID == tvdbID);
            }
        }

        public List<CrossRef_AniDB_TvDB_Episode_Override> GetByAniDBEpisodeID(int id)
        {
            lock (Cache)
            {
                return EpisodeIDs.GetMultiple(id);
            }
        }

        public List<CrossRef_AniDB_TvDB_Episode_Override> GetByAnimeID(int id)
        {
            lock (Cache)
            {
                return AnimeIDs.GetMultiple(id);
            }
        }

        public override void RegenerateDb()
        {
        }

        protected override int SelectKey(CrossRef_AniDB_TvDB_Episode_Override entity)
        {
            return entity.CrossRef_AniDB_TvDB_Episode_OverrideID;
        }
    }
}
