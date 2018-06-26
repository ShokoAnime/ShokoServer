using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class CrossRef_AniDB_TvDB_Episode_OverrideRepository : BaseRepository<CrossRef_AniDB_TvDB_Episode_Override, int>
    {
        private PocoIndex<int, CrossRef_AniDB_TvDB_Episode_Override, int> AnimeIDs;
        private PocoIndex<int, CrossRef_AniDB_TvDB_Episode_Override, int> EpisodeIDs;

        internal override void PopulateIndexes()
        {
            AnimeIDs = new PocoIndex<int, CrossRef_AniDB_TvDB_Episode_Override, int>(Cache,
                a => Repo.AniDB_Episode.GetByEpisodeID(a.AniDBEpisodeID)?.AnimeID ?? -1);
            EpisodeIDs = new PocoIndex<int, CrossRef_AniDB_TvDB_Episode_Override, int>(Cache, a => a.AniDBEpisodeID);
        }
        
        public CrossRef_AniDB_TvDB_Episode_Override GetByAniDBAndTvDBEpisodeIDs(int anidbID, int tvdbID)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached) return EpisodeIDs.GetMultiple(anidbID).FirstOrDefault(a => a.TvDBEpisodeID == tvdbID);
                else return Table.Where(s => s.AniDBEpisodeID == anidbID).FirstOrDefault(a => a.TvDBEpisodeID == tvdbID);
            }
        }

        public List<CrossRef_AniDB_TvDB_Episode_Override> GetByAniDBEpisodeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached) return EpisodeIDs.GetMultiple(id);
                return Table.Where(a => a.AniDBEpisodeID == id).ToList();
            }
        }

        public List<CrossRef_AniDB_TvDB_Episode_Override> GetByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached) return AnimeIDs.GetMultiple(id);
                //TODO: Implement
            }
        }

        internal override int SelectKey(CrossRef_AniDB_TvDB_Episode_Override entity)
        {
            return entity.CrossRef_AniDB_TvDB_Episode_OverrideID;
        }

        internal override void ClearIndexes()
        {

        }
    }
}
