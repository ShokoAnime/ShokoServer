using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Shoko.Models;
using Shoko.Models.Server;
using Shoko.Server.Repositories.NHibernate;
using NHibernate;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Models.Client;
using Shoko.Server.Databases;
using Shoko.Server.Entities;
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
                if (!Changes.ContainsKey(cr.JMMUserID))
                    Changes[cr.JMMUserID] = new ChangeTracker<int>();
                Changes[cr.JMMUserID].Remove(cr.AnimeSeriesID);
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
            foreach (int n in Cache.Values.Select(a => a.JMMUserID).Distinct())
            {
                Changes[n] = new ChangeTracker<int>();
                Changes[n].AddOrUpdateRange(Users.GetMultiple(n).Select(a => a.AnimeSeriesID));
            }
            int cnt = 0;
            List<SVR_AnimeSeries_User> sers =
                Cache.Values.Where(a => a.PlexContractVersion < SVR_AnimeGroup_User.PLEXCONTRACT_VERSION).ToList();
            int max = sers.Count;
	        ServerState.Instance.CurrentSetupStatus = string.Format(Shoko.Server.Properties.Resources.Database_Cache, typeof(SVR_AnimeSeries_User).Name, " DbRegen");
	        if (max <= 0) return;
	        foreach (SVR_AnimeSeries_User g in sers)
            {
                Save(g);
                cnt++;
                if (cnt % 10 == 0)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(Shoko.Server.Properties.Resources.Database_Cache, typeof(SVR_AnimeSeries_User).Name,
                        " DbRegen - " + cnt + "/" + max);
                }
            }
            ServerState.Instance.CurrentSetupStatus = string.Format(Shoko.Server.Properties.Resources.Database_Cache, typeof(SVR_AnimeSeries_User).Name,
                " DbRegen - " + max + "/" + max);
        }



        public override void Save(IReadOnlyCollection<SVR_AnimeSeries_User> objs)
        {
            foreach(SVR_AnimeSeries_User s in objs)
                Save(s);
        }


        public override void Save(SVR_AnimeSeries_User obj)
        {
            lock (obj)
            {
                UpdatePlexKodiContracts(obj);
                SVR_AnimeSeries_User old;
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    old = session.Get<SVR_AnimeSeries_User>(obj.AnimeSeries_UserID);
                }
                HashSet<GroupFilterConditionType> types = SVR_AnimeSeries_User.GetConditionTypesChanged(old, obj);
                base.Save(obj);
                if (!Changes.ContainsKey(obj.JMMUserID))
                    Changes[obj.JMMUserID] = new ChangeTracker<int>();
                Changes[obj.JMMUserID].AddOrUpdate(obj.AnimeSeriesID);
                obj.UpdateGroupFilter(types);
            }
            //logger.Trace("Updating group stats by series from AnimeSeries_UserRepository.Save: {0}", obj.AnimeSeriesID);
            //StatsCache.Instance.UpdateUsingSeries(obj.AnimeSeriesID);
        }

        private void UpdatePlexKodiContracts(SVR_AnimeSeries_User ugrp)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(ugrp.AnimeSeriesID);
                CL_AnimeSeries_User con = ser?.GetUserContract(ugrp.JMMUserID);
                if (con == null)
                    return;
                ugrp.PlexContract = Helper.GenerateFromSeries(con, ser, ser.GetAnime(), ugrp.JMMUserID);
            }
        }





        public SVR_AnimeSeries_User GetByUserAndSeriesID(int userid, int seriesid)
        {
            return UsersSeries.GetOne(userid, seriesid);
        }


        public List<SVR_AnimeSeries_User> GetByUserID(int userid)
        {
            return Users.GetMultiple(userid);
        }

        public List<SVR_AnimeSeries_User> GetBySeriesID(int seriesid)
        {
            return Series.GetMultiple(seriesid);
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
            if (Changes.ContainsKey(userid))
                return Changes[userid];
            return new ChangeTracker<int>();
        }

    }
}