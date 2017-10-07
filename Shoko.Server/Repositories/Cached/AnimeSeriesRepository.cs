using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Shoko.Models.Server;
using NHibernate;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Cached
{
    public class AnimeSeriesRepository : BaseCachedRepository<SVR_AnimeSeries, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PocoIndex<int, SVR_AnimeSeries, int> AniDBIds;
        private PocoIndex<int, SVR_AnimeSeries, int> Groups;

        private ChangeTracker<int> Changes = new ChangeTracker<int>();

        private AnimeSeriesRepository()
        {
            BeginDeleteCallback = (cr) =>
            {
                RepoFactory.AnimeSeries_User.Delete(RepoFactory.AnimeSeries_User.GetBySeriesID(cr.AnimeSeriesID));
                lock (Changes)
                {
                    Changes.Remove(cr.AnimeSeriesID);
                }
            };
            EndDeleteCallback = (cr) =>
            {
                cr.DeleteFromFilters();
                if (cr.AnimeGroupID > 0)
                {
                    logger.Trace("Updating group stats by group from AnimeSeriesRepository.Delete: {0}",
                        cr.AnimeGroupID);
                    SVR_AnimeGroup oldGroup = RepoFactory.AnimeGroup.GetByID(cr.AnimeGroupID);
                    if (oldGroup != null)
                        RepoFactory.AnimeGroup.Save(oldGroup, true, true);
                }
            };
        }

        public static AnimeSeriesRepository Create()
        {
            return new AnimeSeriesRepository();
        }

        protected override int SelectKey(SVR_AnimeSeries entity)
        {
            return entity.AnimeSeriesID;
        }

        public override void PopulateIndexes()
        {
            Changes.AddOrUpdateRange(Cache.Keys);
            AniDBIds = Cache.CreateIndex(a => a.AniDB_ID);
            Groups = Cache.CreateIndex(a => a.AnimeGroupID);
        }

        public override void RegenerateDb()
        {
            try
            {
                int cnt = 0;
                List<SVR_AnimeSeries> sers =
                    Cache.Values.Where(
                            a => a.ContractVersion < SVR_AnimeSeries.CONTRACT_VERSION ||
                                 a.Contract?.AniDBAnime?.AniDBAnime == null)
                        .ToList();
                int max = sers.Count;
                ServerState.Instance.CurrentSetupStatus = string.Format(
                    Commons.Properties.Resources.Database_Cache, typeof(AnimeSeries).Name, " DbRegen");
                if (max <= 0) return;
                foreach (SVR_AnimeSeries s in sers)
                {
                    try
                    {
                        Save(s, false, false, true);
                    }
                    catch
                    {
                    }

                    cnt++;
                    if (cnt % 10 == 0)
                    {
                        ServerState.Instance.CurrentSetupStatus = string.Format(
                            Commons.Properties.Resources.Database_Cache, typeof(AnimeSeries).Name,
                            " DbRegen - " + cnt + "/" + max);
                    }
                }
                ServerState.Instance.CurrentSetupStatus = string.Format(
                    Commons.Properties.Resources.Database_Cache, typeof(AnimeSeries).Name,
                    " DbRegen - " + max + "/" + max);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public ChangeTracker<int> GetChangeTracker()
        {
            lock (Changes)
            {
                return Changes;
            }
        }

        public override void Save(SVR_AnimeSeries obj)
        {
            Save(obj, false);
        }

        public void Save(SVR_AnimeSeries obj, bool onlyupdatestats)
        {
            Save(obj, true, onlyupdatestats);
        }

        public void Save(SVR_AnimeSeries obj, bool updateGroups, bool onlyupdatestats, bool skipgroupfilters = false,
            bool alsoupdateepisodes = false)
        {
            bool newSeries = false;
            SVR_AnimeGroup oldGroup = null;
            bool isMigrating = false;
            lock (obj)
            {
                if (obj.AnimeSeriesID == 0)
                    newSeries = true; // a new series
                else
                {
                    // get the old version from the DB
                    SVR_AnimeSeries oldSeries;
                    using (var session = DatabaseFactory.SessionFactory.OpenSession())
                    {
                        lock (globalDBLock)
                        {
                            oldSeries = session.Get<SVR_AnimeSeries>(obj.AnimeSeriesID);
                        }
                    }
                    if (oldSeries != null)
                    {
                        // means we are moving series to a different group
                        if (oldSeries.AnimeGroupID != obj.AnimeGroupID)
                        {
                            oldGroup = RepoFactory.AnimeGroup.GetByID(oldSeries.AnimeGroupID);
                            SVR_AnimeGroup newGroup = RepoFactory.AnimeGroup.GetByID(obj.AnimeGroupID);
                            if (newGroup != null && newGroup.GroupName.Equals("AAA Migrating Groups AAA"))
                                isMigrating = true;
                            newSeries = true;
                        }
                    }
                }
                if (newSeries && !isMigrating)
                {
                    obj.Contract = null;
                    base.Save(obj);
                }
                HashSet<GroupFilterConditionType> types = obj.UpdateContract(onlyupdatestats);
                base.Save(obj);
                if (!skipgroupfilters && !isMigrating)
                {
                    int endyear = obj.Contract.AniDBAnime.AniDBAnime.EndYear;
                    if (endyear == 0) endyear = DateTime.Today.Year;
                    HashSet<int> allyears = null;
                    if (obj.Contract.AniDBAnime.AniDBAnime.BeginYear != 0)
                    {
                        allyears = new HashSet<int>(Enumerable.Range(obj.Contract.AniDBAnime.AniDBAnime.BeginYear,
                            endyear - obj.Contract.AniDBAnime.AniDBAnime.BeginYear + 1));
                    }
                    RepoFactory.GroupFilter.CreateOrVerifyDirectoryFilters(false,
                        obj.Contract?.AniDBAnime?.AniDBAnime?.GetAllTags(), allyears);
                    //This call will create extra years or tags if the Group have a new year or tag
                    obj.UpdateGroupFilters(types, null);
                }
                lock (Changes)
                {
                    Changes.AddOrUpdate(obj.AnimeSeriesID);
                }
            }
            if (updateGroups && !isMigrating)
            {
                logger.Trace("Updating group stats by series from AnimeSeriesRepository.Save: {0}", obj.AnimeSeriesID);
                SVR_AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(obj.AnimeGroupID);
                if (grp != null)
                    RepoFactory.AnimeGroup.Save(grp, true, true);

                if (oldGroup != null)
                {
                    logger.Trace("Updating group stats by group from AnimeSeriesRepository.Save: {0}",
                        oldGroup.AnimeGroupID);
                    RepoFactory.AnimeGroup.Save(oldGroup, true, true);
                }
            }
            if (alsoupdateepisodes)
            {
                List<SVR_AnimeEpisode> eps = RepoFactory.AnimeEpisode.GetBySeriesID(obj.AnimeSeriesID);
                RepoFactory.AnimeEpisode.Save(eps);
            }
        }

        public void UpdateBatch(ISessionWrapper session, IReadOnlyCollection<SVR_AnimeSeries> seriesBatch)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (seriesBatch == null)
                throw new ArgumentNullException(nameof(seriesBatch));

            if (seriesBatch.Count == 0)
            {
                return;
            }

            foreach (SVR_AnimeSeries series in seriesBatch)
            {
                lock (globalDBLock)
                {
                    session.Update(series);
                    lock (Cache)
                    {
                        Cache.Update(series);
                    }
                }
                lock (Changes)
                {
                    Changes.AddOrUpdate(series.AnimeSeriesID);
                }
            }
        }

        public SVR_AnimeSeries GetByAnimeID(int id)
        {
            lock (Cache)
            {
                return AniDBIds.GetOne(id);
            }
        }


        public List<SVR_AnimeSeries> GetByGroupID(int groupid)
        {
            lock (Cache)
            {
                return Groups.GetMultiple(groupid);
            }
        }


        public List<SVR_AnimeSeries> GetWithMissingEpisodes()
        {
            lock (Cache)
            {
                return
                    Cache.Values.Where(a => a.MissingEpisodeCountGroups > 0)
                        .OrderByDescending(a => a.EpisodeAddedDate)
                        .ToList();
            }
        }

        public List<SVR_AnimeSeries> GetMostRecentlyAdded(int maxResults)
        {
            lock (Cache)
            {
                return Cache.Values.OrderByDescending(a => a.DateTimeCreated).Take(maxResults + 15).ToList();
            }
        }
    }
}