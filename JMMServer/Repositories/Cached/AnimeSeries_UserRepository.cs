using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using JMMContracts;
using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.PlexAndKodi;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NLog;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories.Cached
{
    public class AnimeSeries_UserRepository : BaseCachedRepository<AnimeSeries_User, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PocoIndex<int, AnimeSeries_User, int> Users;
        private PocoIndex<int, AnimeSeries_User, int> Series;
        private PocoIndex<int, AnimeSeries_User, int, int> UsersSeries;
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

        protected override int SelectKey(AnimeSeries_User entity)
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
            List<AnimeSeries_User> sers =
                Cache.Values.Where(a => a.PlexContractVersion < AnimeGroup_User.PLEXCONTRACT_VERSION).ToList();
            int max = sers.Count;
            foreach (AnimeSeries_User g in sers)
            {
                Save(g);
                cnt++;
                if (cnt % 10 == 0)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, typeof(AnimeSeries_User).Name,
                        " DbRegen - " + cnt + "/" + max);
                }
            }
            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, typeof(AnimeSeries_User).Name,
                " DbRegen - " + max + "/" + max);
        }



        public override void Save(IReadOnlyCollection<AnimeSeries_User> objs)
        {
            foreach(AnimeSeries_User s in objs)
                Save(s);
        }


        public override void Save(AnimeSeries_User obj)
        {
            lock (obj)
            {
                UpdatePlexKodiContracts(obj);
                AnimeSeries_User old;
                using (var session = DatabaseFactory.SessionFactory.OpenSession())
                {
                    old = session.Get<AnimeSeries_User>(obj.AnimeSeries_UserID);
                }
                HashSet<GroupFilterConditionType> types = AnimeSeries_User.GetConditionTypesChanged(old, obj);
                base.Save(obj);
                if (!Changes.ContainsKey(obj.JMMUserID))
                    Changes[obj.JMMUserID] = new ChangeTracker<int>();
                Changes[obj.JMMUserID].AddOrUpdate(obj.AnimeSeriesID);
                obj.UpdateGroupFilter(types);
            }
            //logger.Trace("Updating group stats by series from AnimeSeries_UserRepository.Save: {0}", obj.AnimeSeriesID);
            //StatsCache.Instance.UpdateUsingSeries(obj.AnimeSeriesID);
        }

        private void UpdatePlexKodiContracts(AnimeSeries_User ugrp)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(ugrp.AnimeSeriesID);
                Contract_AnimeSeries con = ser?.GetUserContract(ugrp.JMMUserID);
                if (con == null)
                    return;
                ugrp.PlexContract = Helper.GenerateFromSeries(con, ser, ser.GetAnime(), ugrp.JMMUserID);
            }
        }





        public AnimeSeries_User GetByUserAndSeriesID(int userid, int seriesid)
        {
            return UsersSeries.GetOne(userid, seriesid);
        }


        public List<AnimeSeries_User> GetByUserID(int userid)
        {
            return Users.GetMultiple(userid);
        }

        public List<AnimeSeries_User> GetBySeriesID(int seriesid)
        {
            return Series.GetMultiple(seriesid);
        }



        public List<AnimeSeries_User> GetMostRecentlyWatched(int userID)
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