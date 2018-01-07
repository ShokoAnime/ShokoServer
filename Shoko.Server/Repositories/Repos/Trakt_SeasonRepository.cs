using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Repos
{
    public class Trakt_SeasonRepository : BaseRepository<Trakt_Season, int>
    {
        private PocoIndex<int, Trakt_Season, int> Shows;
        private PocoIndex<int, Trakt_Season, int, int> ShowsSeasons;

        internal override int SelectKey(Trakt_Season entity) => entity.Trakt_SeasonID;
        
        internal override void PopulateIndexes()
        {
            Shows = new PocoIndex<int, Trakt_Season, int>(Cache, a => a.Trakt_ShowID);
            ShowsSeasons = new PocoIndex<int, Trakt_Season, int, int>(Cache, a => a.Trakt_ShowID, a => a.Season);
        }

        internal override void ClearIndexes()
        {
            Shows = null;
            ShowsSeasons = null;
        }
        public List<Trakt_Season> GetByShowID(int showID)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Shows.GetMultiple(showID);
                return Table.Where(a => a.Trakt_ShowID == showID).ToList();
            }
        }

        public List<Trakt_Season> GetByShowIDAndSeason(int showID, int seasonNumber)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return ShowsSeasons.GetMultiple(showID, seasonNumber);
                return Table.Where(a => a.Trakt_ShowID == showID && a.Season == seasonNumber).ToList();
            }
        }

    }
}