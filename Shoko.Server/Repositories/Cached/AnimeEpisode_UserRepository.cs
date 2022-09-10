using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Properties;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Cached
{
    public class AnimeEpisode_UserRepository : BaseCachedRepository<SVR_AnimeEpisode_User, int>
    {
        private PocoIndex<int, SVR_AnimeEpisode_User, (int UserID, int EpisodeID)> UsersEpisodes;
        private PocoIndex<int, SVR_AnimeEpisode_User, int> Users;
        private PocoIndex<int, SVR_AnimeEpisode_User, int> Episodes;
        private PocoIndex<int, SVR_AnimeEpisode_User, (int UserID, int SeriesID)> UsersSeries;

        protected override int SelectKey(SVR_AnimeEpisode_User entity)
        {
            return entity.AnimeEpisode_UserID;
        }

        public override void PopulateIndexes()
        {
            UsersEpisodes = Cache.CreateIndex(a => (a.JMMUserID, a.AnimeEpisodeID));
            Users = Cache.CreateIndex(a => a.JMMUserID);
            Episodes = Cache.CreateIndex(a => a.AnimeEpisodeID);
            UsersSeries = Cache.CreateIndex(a => (a.JMMUserID, a.AnimeSeriesID));
        }

        public override void RegenerateDb()
        {
            var cnt = 0;
            var sers =
                Cache.Values.Where(a => a.AnimeEpisode_UserID == 0)
                    .ToList();
            var max = sers.Count;
            ServerState.Instance.ServerStartingStatus = string.Format(Resources.Database_Validating,
                typeof(AnimeEpisode_User).Name, " DbRegen");
            if (max <= 0) return;
            foreach (var g in sers)
            {
                Save(g);
                cnt++;
                if (cnt % 10 == 0)
                    ServerState.Instance.ServerStartingStatus = string.Format(
                        Resources.Database_Validating, typeof(AnimeEpisode_User).Name,
                        " DbRegen - " + cnt + "/" + max);
            }
            ServerState.Instance.ServerStartingStatus = string.Format(Resources.Database_Validating,
                typeof(AnimeEpisode_User).Name,
                " DbRegen - " + max + "/" + max);
        }

        public SVR_AnimeEpisode_User GetByUserIDAndEpisodeID(int userid, int epid)
        {
            Lock.EnterReadLock();
            var result = UsersEpisodes.GetOne((userid, epid));
            Lock.ExitReadLock();
            return result;
        }


        public List<SVR_AnimeEpisode_User> GetByUserID(int userid)
        {
            Lock.EnterReadLock();
            var result = Users.GetMultiple(userid);
            Lock.ExitReadLock();
            return result;
        }

        public List<SVR_AnimeEpisode_User> GetMostRecentlyWatched(int userid, int maxresults = 100)
        {
            return GetByUserID(userid).Where(a => a.WatchedCount > 0).OrderByDescending(a => a.WatchedDate)
                .Take(maxresults).ToList();
        }

        public SVR_AnimeEpisode_User GetLastWatchedEpisodeForSeries(int seriesid, int userid)
        {
            return GetByUserIDAndSeriesID(userid, seriesid).Where(a => a.WatchedCount > 0)
                .OrderByDescending(a => a.WatchedDate).FirstOrDefault();
        }

        public List<SVR_AnimeEpisode_User> GetByEpisodeID(int epid)
        {
            Lock.EnterReadLock();
            var result = Episodes.GetMultiple(epid);
            Lock.ExitReadLock();
            return result;
        }

        public List<SVR_AnimeEpisode_User> GetByUserIDAndSeriesID(int userid, int seriesid)
        {
            Lock.EnterReadLock();
            var result = UsersSeries.GetMultiple((userid, seriesid));
            Lock.ExitReadLock();
            return result;
        }
    }
}
