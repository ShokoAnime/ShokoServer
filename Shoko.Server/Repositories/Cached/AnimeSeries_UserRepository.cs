using System.Collections.Generic;
using System.Linq;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.PlexAndKodi;

namespace Shoko.Server.Repositories.Cached
{
    public class AnimeSeries_UserRepository : BaseCachedRepository<SVR_AnimeSeries_User, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PocoIndex<int, SVR_AnimeSeries_User, int> Users;
        private PocoIndex<int, SVR_AnimeSeries_User, int> Series;
        private PocoIndex<int, SVR_AnimeSeries_User, int, int> UsersSeries;
        private Dictionary<int, ChangeTracker<int>> Changes = new Dictionary<int, ChangeTracker<int>>();


        private AnimeSeries_UserRepository()
        {
            EndDeleteCallback = (cr) =>
            {
                lock (Changes)
                {
                    if (!Changes.ContainsKey(cr.JMMUserID))
                        Changes[cr.JMMUserID] = new ChangeTracker<int>();
                    Changes[cr.JMMUserID].Remove(cr.AnimeSeriesID);
                }
                cr.DeleteFromFilters();
            };
        }

        public static AnimeSeries_UserRepository Create()
        {
            return new AnimeSeries_UserRepository();
        }

        protected override int SelectKey(SVR_AnimeSeries_User entity)
        {
            return entity.AnimeSeries_UserID;
        }

        public override void PopulateIndexes()
        {
            Users = Cache.CreateIndex(a => a.JMMUserID);
            Series = Cache.CreateIndex(a => a.AnimeSeriesID);
            UsersSeries = Cache.CreateIndex(a => a.JMMUserID, a => a.AnimeSeriesID);
        }

        public override void RegenerateDb()
        {
        }


        public override void Save(SVR_AnimeSeries_User obj)
        {
            lock (obj)
            {
                UpdatePlexKodiContracts(obj);
                SVR_AnimeSeries_User old;
                lock (globalDBLock)
                {
                    using (var session = DatabaseFactory.SessionFactory.OpenSession())
                    {
                        old = session.Get<SVR_AnimeSeries_User>(obj.AnimeSeries_UserID);
                    }
                }
                HashSet<GroupFilterConditionType> types = SVR_AnimeSeries_User.GetConditionTypesChanged(old, obj);
                base.Save(obj);
                lock (Changes)
                {
                    if (!Changes.ContainsKey(obj.JMMUserID))
                        Changes[obj.JMMUserID] = new ChangeTracker<int>();
                    Changes[obj.JMMUserID].AddOrUpdate(obj.AnimeSeriesID);
                }
                obj.UpdateGroupFilter(types);
            }
            //logger.Trace("Updating group stats by series from AnimeSeries_UserRepository.Save: {0}", obj.AnimeSeriesID);
            //StatsCache.Instance.UpdateUsingSeries(obj.AnimeSeriesID);
        }

        private void UpdatePlexKodiContracts(SVR_AnimeSeries_User ugrp)
        {
            SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(ugrp.AnimeSeriesID);
            CL_AnimeSeries_User con = ser?.GetUserContract(ugrp.JMMUserID);
            if (con == null)
                return;
            ugrp.PlexContract = Helper.GenerateFromSeries(con, ser, ser.GetAnime(), ugrp.JMMUserID);
        }


        public SVR_AnimeSeries_User GetByUserAndSeriesID(int userid, int seriesid)
        {
            lock (Cache)
            {
                return UsersSeries.GetOne(userid, seriesid);
            }
        }

        public List<SVR_AnimeSeries_User> GetByUserID(int userid)
        {
            lock (Cache)
            {
                return Users.GetMultiple(userid);
            }
        }

        public List<SVR_AnimeSeries_User> GetBySeriesID(int seriesid)
        {
            lock (Cache)
            {
                return Series.GetMultiple(seriesid);
            }
        }


        public List<SVR_AnimeSeries_User> GetMostRecentlyWatched(int userID)
        {
            return
                GetByUserID(userID)
                    .Where(a => a.UnwatchedEpisodeCount > 0)
                    .OrderByDescending(a => a.WatchedDate)
                    .ToList();
        }


        public ChangeTracker<int> GetChangeTracker(int userid)
        {
            lock (Changes)
            {
                if (Changes.ContainsKey(userid))
                    return Changes[userid];
            }
            return new ChangeTracker<int>();
        }
    }
}