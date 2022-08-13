using System.Collections.Generic;
using System.Linq;
using NHibernate;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Cached
{
    public class AnimeEpisode_UserRepository : BaseCachedRepository<SVR_AnimeEpisode_User, int>
    {
        private PocoIndex<int, SVR_AnimeEpisode_User, int> Series;
        private PocoIndex<int, SVR_AnimeEpisode_User, ulong> UsersEpisodes;
        private PocoIndex<int, SVR_AnimeEpisode_User, int> Users;
        private PocoIndex<int, SVR_AnimeEpisode_User, int> Episodes;
        private PocoIndex<int, SVR_AnimeEpisode_User, ulong> UsersSeries;

        protected override int SelectKey(SVR_AnimeEpisode_User entity)
        {
            return entity.AnimeEpisode_UserID;
        }

        public override void PopulateIndexes()
        {
            Series = Cache.CreateIndex(a => a.AnimeSeriesID);
            UsersEpisodes = Cache.CreateIndex(a => (ulong) a.JMMUserID << 48 | (ulong) a.AnimeEpisodeID);
            Users = Cache.CreateIndex(a => a.JMMUserID);
            Episodes = Cache.CreateIndex(a => a.AnimeEpisodeID);
            UsersSeries = Cache.CreateIndex(a => (ulong) a.JMMUserID << 48 | (ulong) a.AnimeSeriesID);
        }

        public override void RegenerateDb()
        {
            int cnt = 0;
            List<SVR_AnimeEpisode_User> sers =
                Cache.Values.Where(a => a.AnimeEpisode_UserID == 0)
                    .ToList();
            int max = sers.Count;
            ServerState.Instance.ServerStartingStatus = string.Format(Resources.Database_Validating,
                typeof(AnimeEpisode_User).Name, " DbRegen");
            if (max <= 0) return;
            foreach (SVR_AnimeEpisode_User g in sers)
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

        public List<SVR_AnimeEpisode_User> GetBySeriesID(int seriesid)
        {
            lock (Cache)
            {
                return Series.GetMultiple(seriesid);
            }
        }

        public SVR_AnimeEpisode_User GetByUserIDAndEpisodeID(int userid, int epid)
        {
            lock (Cache)
            {
                return UsersEpisodes.GetOne((ulong) userid << 48 | (ulong) epid);
            }
        }


        public List<SVR_AnimeEpisode_User> GetByUserID(int userid)
        {
            lock (Cache)
            {
                return Users.GetMultiple(userid);
            }
        }

        public List<SVR_AnimeEpisode_User> GetMostRecentlyWatched(int userid, int maxresults = 100)
        {
            return GetByUserID(userid).Where(a => a.WatchedCount > 0).OrderByDescending(a => a.WatchedDate)
                .Take(maxresults).ToList();
        }


        public SVR_AnimeEpisode_User GetLastWatchedEpisode()
        {
            lock (Cache)
            {
                return Cache.Values.Where(a => a.WatchedCount > 0).OrderByDescending(a => a.WatchedDate)
                    .FirstOrDefault();
            }
        }

        public SVR_AnimeEpisode_User GetLastWatchedEpisodeForSeries(int seriesid, int userid)
        {
            lock (Cache)
            {
                return UsersSeries.GetMultiple((ulong) userid << 48 | (ulong) seriesid).Where(a => a.WatchedCount > 0)
                    .OrderByDescending(a => a.WatchedDate).FirstOrDefault();
            }
        }

        public List<SVR_AnimeEpisode_User> GetByEpisodeID(int epid)
        {
            lock (Cache)
            {
                return Episodes.GetMultiple(epid);
            }
        }

        public List<SVR_AnimeEpisode_User> GetByUserIDAndSeriesID(int userid, int seriesid)
        {
            lock (Cache)
            {
                return UsersSeries.GetMultiple((ulong) userid << 48 | (ulong) seriesid);
            }
        }
    }
}
