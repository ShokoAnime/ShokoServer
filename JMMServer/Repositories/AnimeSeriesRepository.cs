using System.Collections.Generic;
using System.Linq;
using JMMServer.Databases;
using JMMServer.Entities;
using NHibernate;
using NLog;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class AnimeSeriesRepository
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        internal static PocoCache<int, AnimeSeries> Cache;
        private static PocoIndex<int, AnimeSeries, int> AniDBIds;
        private static PocoIndex<int, AnimeSeries, int> Groups;

        private static ChangeTracker<int> Changes = new ChangeTracker<int>();

        public static void InitCache()
        {
            string t = "AnimeSeries";
            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t, string.Empty);

            AnimeSeriesRepository repo = new AnimeSeriesRepository();
            Cache = new PocoCache<int, AnimeSeries>(repo.InternalGetAll(), a => a.AnimeSeriesID);
            Changes.AddOrUpdateRange(Cache.Keys);
            AniDBIds = Cache.CreateIndex(a => a.AniDB_ID);
            Groups = Cache.CreateIndex(a => a.AnimeGroupID);
            int cnt = 0;
            List<AnimeSeries> sers =
                Cache.Values.Where(
                    a => a.ContractVersion < AnimeSeries.CONTRACT_VERSION || a.Contract?.AniDBAnime?.AniDBAnime == null)
                    .ToList();
            int max = sers.Count;
            foreach (AnimeSeries s in sers)
            {
                repo.Save(s, false, false, true);
                cnt++;
                if (cnt%10 == 0)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t,
                        " DbRegen - " + cnt + "/" + max);
                }
            }
            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t,
                " DbRegen - " + max + "/" + max);
        }

        private List<AnimeSeries> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var series = session
                    .CreateCriteria(typeof(AnimeSeries))
                    .List<AnimeSeries>();

                return new List<AnimeSeries>(series);
            }
        }


        public void Save(AnimeSeries obj, bool onlyupdatestats)
        {
            Save(obj, true, onlyupdatestats);
        }
        public static ChangeTracker<int> GetChangeTracker()
        {
            return Changes;
        }
        public void Save(AnimeSeries obj, bool updateGroups, bool onlyupdatestats, bool skipgroupfilters = false)
        {
            bool newSeries = false;
            AnimeGroup oldGroup = null;
            AnimeGroupRepository repGroups = new AnimeGroupRepository();
			bool isMigrating = false;
            lock (obj)
            {
                if (obj.AnimeSeriesID == 0)
                    newSeries = true; // a new series
                else
                {
                    // get the old version from the DB
                    AnimeSeries oldSeries;
                    using (var session = JMMService.SessionFactory.OpenSession())
                    {
                        oldSeries = session.Get<AnimeSeries>(obj.AnimeSeriesID);
                    }
                    if (oldSeries != null)
                    {
                        // means we are moving series to a different group
                        if (oldSeries.AnimeGroupID != obj.AnimeGroupID)
                        {
                            oldGroup = repGroups.GetByID(oldSeries.AnimeGroupID);
							AnimeGroup newGroup = repGroups.GetByID(obj.AnimeGroupID);
							if (newGroup != null && newGroup.GroupName.Equals("AAA Migrating Groups AAA"))
								isMigrating = true;
                            newSeries = true;
                        }
                    }
                }
                if (newSeries && !isMigrating)
                {
                    using (var session = JMMService.SessionFactory.OpenSession())
                    {
                        obj.Contract = null;
                        // populate the database
                        using (var transaction = session.BeginTransaction())
                        {
                            session.SaveOrUpdate(obj);
                            transaction.Commit();
                        }
                    }
                }
                HashSet<GroupFilterConditionType> types = obj.UpdateContract(onlyupdatestats);
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    // populate the database
                    using (var transaction = session.BeginTransaction())
                    {
                        session.SaveOrUpdate(obj);
                        transaction.Commit();
                    }
                }
                if (!skipgroupfilters && !isMigrating)
                {
                    GroupFilterRepository.CreateOrVerifyTagsAndYearsFilters(false,
                        obj.Contract?.AniDBAnime?.AniDBAnime?.AllTags, obj.Contract?.AniDBAnime?.AniDBAnime?.AirDate);
                    //This call will create extra years or tags if the Group have a new year or tag
                    obj.UpdateGroupFilters(types, null);
                }
                Cache.Update(obj);
                Changes.AddOrUpdate(obj.AnimeSeriesID);
            }
            if (updateGroups && !isMigrating)
            {
                if (newSeries)
                {
                    logger.Trace("Updating group stats by series from AnimeSeriesRepository.Save: {0}",
                        obj.AnimeSeriesID);
                    AnimeGroup grp = repGroups.GetByID(obj.AnimeGroupID);
                    if (grp != null)
                        repGroups.Save(grp, true, true);
                }

                if (oldGroup != null)
                {
                    logger.Trace("Updating group stats by group from AnimeSeriesRepository.Save: {0}",
                        oldGroup.AnimeGroupID);
                    repGroups.Save(oldGroup, true, true);
                }
            }
        }

        public AnimeSeries GetByID(int id)
        {
            return Cache.Get(id);
        }

        public AnimeSeries GetByID(ISession session, int id)
        {
            return GetByID(id);
        }

        public AnimeSeries GetByAnimeID(int id)
        {
            return AniDBIds.GetOne(id);
        }

        public AnimeSeries GetByAnimeID(ISession session, int id)
        {
            return GetByAnimeID(id);
        }

        public List<AnimeSeries> GetAll()
        {
            return Cache.Values.ToList();
        }

        public List<AnimeSeries> GetAll(ISession session)
        {
            return GetAll();
        }

        public List<AnimeSeries> GetByGroupID(int groupid)
        {
            return Groups.GetMultiple(groupid);
        }

        public List<AnimeSeries> GetByGroupID(ISession session, int groupid)
        {
            return GetByGroupID(groupid);
        }

        public List<AnimeSeries> GetWithMissingEpisodes()
        {
            return
                Cache.Values.Where(a => a.MissingEpisodeCountGroups > 0)
                    .OrderByDescending(a => a.EpisodeAddedDate)
                    .ToList();
        }

        public List<AnimeSeries> GetMostRecentlyAdded(int maxResults)
        {
            return Cache.Values.OrderByDescending(a => a.DateTimeCreated).Take(maxResults + 15).ToList();
        }

        public List<AnimeSeries> GetMostRecentlyAdded(ISession session, int maxResults)
        {
            return GetMostRecentlyAdded(maxResults);
        }

        public void Delete(int id)
        {
            int oldGroupID = 0;
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    AnimeSeries cr = GetByID(id);
                    if (cr != null)
                    {
                        Cache.Remove(cr);
                        // delete user records
                        AnimeSeries_UserRepository repUsers = new AnimeSeries_UserRepository();
                        foreach (AnimeSeries_User grpUser in repUsers.GetBySeriesID(id))
                            repUsers.Delete(grpUser.AnimeSeries_UserID);
                        Changes.Remove(cr.AnimeSeriesID);
                        oldGroupID = cr.AnimeGroupID;
                        session.Delete(cr);
                        transaction.Commit();
                        cr.DeleteFromFilters();
                    }
                }
            }
            if (oldGroupID > 0)
            {
                logger.Trace("Updating group stats by group from AnimeSeriesRepository.Delete: {0}", oldGroupID);
                AnimeGroupRepository repGroups = new AnimeGroupRepository();
                AnimeGroup oldGroup = repGroups.GetByID(oldGroupID);
                if (oldGroup != null)
                    repGroups.Save(oldGroup, true, true);
            }
        }
    }
}