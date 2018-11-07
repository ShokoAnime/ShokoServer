using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class Trakt_EpisodeRepository : BaseRepository<Trakt_Episode, int>
    {
        private PocoIndex<int, Trakt_Episode, int> Shows;
        private PocoIndex<int, Trakt_Episode, int, int> ShowsSeasons;

        internal override int SelectKey(Trakt_Episode entity) => entity.Trakt_EpisodeID;
            
        internal override void PopulateIndexes()
        {
            Shows = new PocoIndex<int, Trakt_Episode, int>(Cache, a => a.Trakt_ShowID);
            ShowsSeasons = new PocoIndex<int, Trakt_Episode,int, int>(Cache, a => a.Trakt_ShowID,a=>a.Season);
        }

        internal override void ClearIndexes()
        {
            Shows = null;
            ShowsSeasons = null;
        }

        public List<Trakt_Episode> GetByShowID(int showID)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Shows.GetMultiple(showID);
                return Table.Where(a => a.Trakt_ShowID == showID).ToList();
            }
        }

        public List<Trakt_Episode> GetByShowIDAndSeason(int showID, int seasonNumber)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return ShowsSeasons.GetMultiple(showID,seasonNumber);
                return Table.Where(a => a.Trakt_ShowID == showID && a.Season==seasonNumber).ToList();
            }
        }

        public Trakt_Episode GetByShowIDSeasonAndEpisode(int showID, int seasonNumber, int epnumber)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return ShowsSeasons.GetMultiple(showID, seasonNumber).FirstOrDefault(a=>a.EpisodeNumber==epnumber);
                return Table.FirstOrDefault(a => a.Trakt_ShowID == showID && a.Season == seasonNumber && a.EpisodeNumber==epnumber);
            }
        }

        public List<int> GetSeasonNumbersForSeries(int showID)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Shows.GetMultiple(showID).Select(a=>a.Season).Distinct().OrderBy(a=>a).ToList();
                return Table.Where(a => a.Trakt_ShowID == showID).ToList().Select(a => a.Season).Distinct().OrderBy(a=>a).ToList();
            }
        }
    }
}