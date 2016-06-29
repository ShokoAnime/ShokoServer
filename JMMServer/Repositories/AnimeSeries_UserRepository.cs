using System.Collections.Generic;
using System.Linq;
using JMMContracts;
using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.PlexAndKodi;
using NHibernate;
using NLog;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class AnimeSeries_UserRepository
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        internal static PocoCache<int, AnimeSeries_User> Cache;
        private static PocoIndex<int, AnimeSeries_User, int> Users;
        private static PocoIndex<int, AnimeSeries_User, int> Series;
        private static PocoIndex<int, AnimeSeries_User, int, int> UsersSeries;
        private static Dictionary<int, ChangeTracker<int>> Changes = new Dictionary<int, ChangeTracker<int>>();

        public static void InitCache()
        {
            string t = "AnimeSeries_User";
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, string.Empty);
            AnimeSeries_UserRepository repo = new AnimeSeries_UserRepository();
            Cache = new PocoCache<int, AnimeSeries_User>(repo.InternalGetAll(), a => a.AnimeSeries_UserID);
            Users = Cache.CreateIndex(a => a.JMMUserID);
            Series = Cache.CreateIndex(a => a.AnimeSeriesID);
            UsersSeries = Cache.CreateIndex(a => a.JMMUserID, a => a.AnimeSeriesID);
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
                repo.Save(g);
                cnt++;
                if (cnt%10 == 0)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t,
                        " DbRegen - " + cnt + "/" + max);
                }
            }
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t,
                " DbRegen - " + max + "/" + max);


        }


        private List<AnimeSeries_User> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var grps = session
                    .CreateCriteria(typeof(AnimeSeries_User))
                    .List<AnimeSeries_User>();

                return new List<AnimeSeries_User>(grps);
            }
        }

        public void Save(AnimeSeries_User obj)
        {
            lock (obj)
            {
                UpdatePlexKodiContracts(obj);
                AnimeSeries_User old;
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    old = session.Get<AnimeSeries_User>(obj.AnimeSeries_UserID);
                }
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    HashSet<GroupFilterConditionType> types = AnimeSeries_User.GetConditionTypesChanged(old, obj);
                    // populate the database
                    using (var transaction = session.BeginTransaction())
                    {
                        session.SaveOrUpdate(obj);
                        transaction.Commit();
                    }
                    Cache.Update(obj);
                    if (!Changes.ContainsKey(obj.JMMUserID))
                        Changes[obj.JMMUserID] = new ChangeTracker<int>();
                    Changes[obj.JMMUserID].AddOrUpdate(obj.AnimeSeriesID);
                    obj.UpdateGroupFilter(types);
                }
            }
            //logger.Trace("Updating group stats by series from AnimeSeries_UserRepository.Save: {0}", obj.AnimeSeriesID);
            //StatsCache.Instance.UpdateUsingSeries(obj.AnimeSeriesID);
        }

        private void UpdatePlexKodiContracts(AnimeSeries_User ugrp)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AnimeSeriesRepository repo = new AnimeSeriesRepository();
                AnimeSeries ser = repo.GetByID(ugrp.AnimeSeriesID);
                Contract_AnimeSeries con = ser?.GetUserContract(ugrp.JMMUserID);
                if (con == null)
                    return;
                ugrp.PlexContract = Helper.GenerateFromSeries(con, ser, ser.GetAnime(session), ugrp.JMMUserID);
            }
        }

        public AnimeSeries_User GetByID(int id)
        {
            return Cache.Get(id);
        }

        public AnimeSeries_User GetByUserAndSeriesID(int userid, int seriesid)
        {
            return UsersSeries.GetOne(userid, seriesid);
        }

        public AnimeSeries_User GetByUserAndSeriesID(ISession session, int userid, int seriesid)
        {
            return GetByUserAndSeriesID(userid, seriesid);
        }

        public List<AnimeSeries_User> GetByUserID(int userid)
        {
            return Users.GetMultiple(userid);
        }

        public List<AnimeSeries_User> GetBySeriesID(int seriesid)
        {
            return Series.GetMultiple(seriesid);
        }

        public List<AnimeSeries_User> GetAll()
        {
            return Cache.Values.ToList();
        }

        public List<AnimeSeries_User> GetMostRecentlyWatched(int userID)
        {
            return
                GetByUserID(userID)
                    .Where(a => a.UnwatchedEpisodeCount > 0)
                    .OrderByDescending(a => a.WatchedDate)
                    .ToList();
        }

        public List<AnimeSeries_User> GetMostRecentlyWatched(ISession session, int userID)
        {
            return GetMostRecentlyWatched(userID);
        }

        public static ChangeTracker<int> GetChangeTracker(int userid)
        {
            if (Changes.ContainsKey(userid))
                return Changes[userid];
            return new ChangeTracker<int>();
        }
        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    AnimeSeries_User cr = GetByID(id);
                    if (cr != null)
                    {
                        Cache.Remove(cr);
                        if (!Changes.ContainsKey(cr.JMMUserID))
                            Changes[cr.JMMUserID] = new ChangeTracker<int>();
                        Changes[cr.JMMUserID].Remove(cr.AnimeSeriesID);
                        session.Delete(cr);
                        transaction.Commit();
                        cr.DeleteFromFilters();
                    }
                }
            }
        }
    }
}