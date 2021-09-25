using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Cached
{
    public class CrossRef_AniDB_Episode_OverrideRepository : BaseCachedRepository<CrossRef_AniDB_Episode_Override, int>
    {
        private PocoIndex<int, CrossRef_AniDB_Episode_Override, int, string> AnimeIDs;
        private PocoIndex<int, CrossRef_AniDB_Episode_Override, int, string> EpisodeIDs;
        public override void PopulateIndexes()
        {
            AnimeIDs = new PocoIndex<int, CrossRef_AniDB_Episode_Override, int, string>(Cache,
                a => RepoFactory.AniDB_Episode.GetByEpisodeID(a.AniDBEpisodeID)?.AnimeID ?? -1, a=>a.Provider);
            EpisodeIDs = new PocoIndex<int, CrossRef_AniDB_Episode_Override, int, string>(Cache, a => a.AniDBEpisodeID, a => a.Provider);
        }

        public CrossRef_AniDB_Episode_Override GetByAniDBAndProviderEpisodeIDs(int anidbID, string provider, string providerEpisodeId)
        {
            lock (Cache)
            {
                return EpisodeIDs.GetMultiple(anidbID, provider).FirstOrDefault(a => a.ProviderEpisodeID==providerEpisodeId);
            }
        }

        public List<CrossRef_AniDB_Episode_Override> GetByAniDBEpisodeID(int id, string provider)
        {
            lock (Cache)
            {
                return EpisodeIDs.GetMultiple(id, provider);
            }
        }

        public List<CrossRef_AniDB_Episode_Override> GetByAnimeID(int id, string provider)
        {
            lock (Cache)
            {
                return AnimeIDs.GetMultiple(id, provider);
            }
        }

        public override void RegenerateDb()
        {
        }

        protected override int SelectKey(CrossRef_AniDB_Episode_Override entity)
        {
            return entity.CrossRef_AniDB_OverrideID;
        }
    }
}
