using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class CrossRef_AniDB_TvDB_EpisodeRepository : BaseRepository<CrossRef_AniDB_TvDB_Episode, int>
    {
        private PocoIndex<int, CrossRef_AniDB_TvDB_Episode, int> AnimeIDs;
        private PocoIndex<int, CrossRef_AniDB_TvDB_Episode, int> EpisodeIDs;

        internal override int SelectKey(CrossRef_AniDB_TvDB_Episode entity) => entity.CrossRef_AniDB_TvDB_EpisodeID;


        internal override void PopulateIndexes()
        {
            AnimeIDs = new PocoIndex<int, CrossRef_AniDB_TvDB_Episode, int>(Cache, a => a.AnimeID);
            EpisodeIDs = new PocoIndex<int, CrossRef_AniDB_TvDB_Episode, int>(Cache, a => a.AniDBEpisodeID);
        }

        internal override void ClearIndexes()
        {
            AnimeIDs = null;
            EpisodeIDs = null;
        }


        public CrossRef_AniDB_TvDB_Episode GetByAniDBEpisodeID(int id)
        {
            // TODO Change this when multiple AniDB <=> TvDB Episode mappings
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return EpisodeIDs.GetOne(id);
                return Table.FirstOrDefault(a => a.AniDBEpisodeID==id);
            }
        }

        public List<CrossRef_AniDB_TvDB_Episode> GetByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return AnimeIDs.GetMultiple(id);
                return Table.Where(a => a.AnimeID==id).ToList();
            }
        }
    }
}