using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class CrossRef_AniDB_Trakt_EpisodeRepository : BaseRepository<CrossRef_AniDB_Trakt_Episode, int>
    {

        private PocoIndex<int, CrossRef_AniDB_Trakt_Episode, int> Animes;
        private PocoIndex<int, CrossRef_AniDB_Trakt_Episode, int> Episodes;

        internal override int SelectKey(CrossRef_AniDB_Trakt_Episode entity) => entity.CrossRef_AniDB_Trakt_EpisodeID;

        internal override void PopulateIndexes()
        {
            Animes = new PocoIndex<int, CrossRef_AniDB_Trakt_Episode, int>(Cache, a => a.AnimeID);
            Episodes = new PocoIndex<int, CrossRef_AniDB_Trakt_Episode, int>(Cache, a => a.AniDBEpisodeID);
        }

        internal override void ClearIndexes()
        {
            Animes = null;
            Episodes = null;
        }

        public List<CrossRef_AniDB_Trakt_Episode> GetByAniDBEpisodeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Episodes.GetMultiple(id);
                return Table.Where(a=>a.AniDBEpisodeID==id).ToList();
            }
        }

        public List<CrossRef_AniDB_Trakt_Episode> GetByAnimeID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Animes.GetMultiple(id);
                return Table.Where(a => a.AnimeID == id).ToList();
            }
        }
    }
}