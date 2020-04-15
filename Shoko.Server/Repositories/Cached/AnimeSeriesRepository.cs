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
                Changes.Remove(cr.AnimeSeriesID);
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
            var repo = new AnimeSeriesRepository();
            RepoFactory.CachedRepositories.Add(repo);
            return repo;
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
                    Commons.Properties.Resources.Database_Validating, typeof(AnimeSeries).Name, " DbRegen");
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
                            Commons.Properties.Resources.Database_Validating, typeof(AnimeSeries).Name,
                            " DbRegen - " + cnt + "/" + max);
                    }
                }
                ServerState.Instance.CurrentSetupStatus = string.Format(
                    Commons.Properties.Resources.Database_Validating, typeof(AnimeSeries).Name,
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
            return Changes;
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
                var seasons = obj.Contract?.AniDBAnime?.Stat_AllSeasons;
                if (seasons == null || seasons.Count == 0)
                {
                    var anime = obj.GetAnime();
                    if (anime != null)
                        RepoFactory.AniDB_Anime.Save(anime);
                }

                HashSet<GroupFilterConditionType> types = obj.UpdateContract(onlyupdatestats);
                base.Save(obj);
                seasons = obj.Contract?.AniDBAnime?.Stat_AllSeasons;

                if (updateGroups && !isMigrating)
                {
                    logger.Trace("Updating group stats by series from AnimeSeriesRepository.Save: {0}",
                        obj.AnimeSeriesID);
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
                if (!skipgroupfilters && !isMigrating)
                {
                    DateTime start = DateTime.Now;
                    int endyear = obj.Contract?.AniDBAnime?.AniDBAnime?.EndYear ?? 0;
                    if (endyear == 0) endyear = DateTime.Today.Year;
                    int startyear = obj.Contract?.AniDBAnime?.AniDBAnime?.BeginYear ?? 0;
                    if (endyear < startyear) endyear = startyear;
                    HashSet<int> allyears = null;
                    if (startyear != 0)
                    {
                        allyears = startyear == endyear
                            ? new HashSet<int> {startyear}
                            : new HashSet<int>(Enumerable.Range(startyear, endyear - startyear + 1));
                    }

                    //This call will create extra years or tags if the Group have a new year or tag
                    RepoFactory.GroupFilter.CreateOrVerifyDirectoryFilters(false,
                        obj.Contract?.AniDBAnime?.AniDBAnime?.GetAllTags(), allyears, seasons);

                    // Update other existing filters
                    obj.UpdateGroupFilters(types);
                    TimeSpan ts = DateTime.Now - start;
                    logger.Trace($"While Saving SERIES {obj.GetSeriesName() ?? obj.AniDB_ID.ToString()} Updated GroupFilters in {ts.Milliseconds}ms");
                }
                Changes.AddOrUpdate(obj.AnimeSeriesID);
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
                Changes.AddOrUpdate(series.AnimeSeriesID);
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

        public List<SVR_AnimeSeries> GetMostRecentlyAdded(int maxResults, int userID)
        {
            lock (Cache)
            {
                return Cache.Values.Where(a => userID == 0 || RepoFactory.JMMUser.GetByID(userID).AllowedSeries(a))
                    .OrderByDescending(a => a.DateTimeCreated).Take(maxResults).ToList();
            }
        }
    }
}
