using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Repos
{
    public class AnimeSeriesRepository : BaseRepository<SVR_AnimeSeries, int, (bool updateGroups, bool onlyupdatestats, bool skipgroupfilters, bool alsoupdateepisodes)>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PocoIndex<int, SVR_AnimeSeries, int> AniDBIds;
        private PocoIndex<int, SVR_AnimeSeries, int> Groups;

        private readonly ChangeTracker<int> Changes = new ChangeTracker<int>();


        internal override object BeginSave(SVR_AnimeSeries entity, SVR_AnimeSeries original_entity,
            (bool updateGroups, bool onlyupdatestats, bool skipgroupfilters, bool alsoupdateepisodes) parameters)
        {
            bool isMigrating = false;
            bool newSeries = false;
            AnimeGroup oldGroup = null;
            if (original_entity != null)
            {
                // means we are moving series to a different group
                if (original_entity.AnimeGroupID != entity.AnimeGroupID)
                {
                    oldGroup = Repo.AnimeGroup.GetByID(original_entity.AnimeGroupID);
                    SVR_AnimeGroup newGroup = Repo.AnimeGroup.GetByID(entity.AnimeGroupID);
                    if (newGroup != null && newGroup.GroupName.Equals("AAA Migrating Groups AAA"))
                        isMigrating = true;
                    newSeries = true;
                }
            }
            else
                newSeries = true;
            HashSet<GroupFilterConditionType> types = entity.UpdateContract(parameters.onlyupdatestats);
            if (newSeries && !isMigrating)
                entity.Contract = null;
            return (isMigrating, oldGroup, types);
        }

        internal override void EndSave(SVR_AnimeSeries entity, object returnFromBeginSave,
            (bool updateGroups, bool onlyupdatestats, bool skipgroupfilters, bool alsoupdateepisodes) parameters)
        {
            (bool isMigrating, SVR_AnimeGroup oldGroup, HashSet<GroupFilterConditionType> types) = ((bool isMigrating, SVR_AnimeGroup oldGroup, HashSet<GroupFilterConditionType> types))returnFromBeginSave;

            if (parameters.updateGroups && !isMigrating)
            {
                logger.Trace("Updating group stats by series from AnimeSeriesRepository.Save: {0}", entity.AnimeSeriesID);
                Repo.AnimeGroup.BeginUpdate(entity.AnimeGroupID)?.Commit((true, true, true));
                if (oldGroup != null)
                {
                    logger.Trace("Updating group stats by group from AnimeSeriesRepository.Save: {0}",oldGroup.AnimeGroupID);
                    Repo.AnimeGroup.BeginUpdate(oldGroup).Commit((true, true, true));
                }
            }
            if (!parameters.skipgroupfilters && !isMigrating)
            {
                int endyear = entity.Contract?.AniDBAnime?.AniDBAnime?.EndYear ?? 0;
                if (endyear == 0) endyear = DateTime.Today.Year;
                HashSet<int> allyears = null;
                if (entity.Contract?.AniDBAnime?.AniDBAnime != null && entity.Contract.AniDBAnime.AniDBAnime.BeginYear != 0)
                {
                    allyears = new HashSet<int>(Enumerable.Range(entity.Contract.AniDBAnime.AniDBAnime.BeginYear,
                        endyear - entity.Contract.AniDBAnime.AniDBAnime.BeginYear + 1));
                }
                //This call will create extra years or tags if the Group have a new year or tag
                Repo.GroupFilter.CreateOrVerifyDirectoryFilters(null, false,
                    entity.Contract?.AniDBAnime?.AniDBAnime?.GetAllTags(), allyears,
                    entity.Contract?.AniDBAnime?.Stat_AllSeasons);

                // Update other existing filters
                entity.UpdateGroupFilters(types);
            }
            lock (Changes)
            {
                Changes.AddOrUpdate(entity.AnimeSeriesID);
            }
            if (parameters.alsoupdateepisodes)
            {
                Repo.AnimeEpisode.BeginUpdate(Repo.AnimeEpisode.GetBySeriesID(entity.AnimeSeriesID))?.Commit();
            }
        }

        internal override object BeginDelete(SVR_AnimeSeries entity,
            (bool updateGroups, bool onlyupdatestats, bool skipgroupfilters, bool alsoupdateepisodes) parameters)
        {
            Repo.AnimeSeries_User.Delete(entity.AnimeSeriesID);
            lock(Changes)
                Changes.Remove(entity.AnimeSeriesID);
            return null;
        }

        internal override void EndDelete(SVR_AnimeSeries entity, object returnFromBeginDelete,
            (bool updateGroups, bool onlyupdatestats, bool skipgroupfilters, bool alsoupdateepisodes) parameters)
        {
            entity.DeleteFromFilters();
            if (entity.AnimeGroupID != 0)
            {
                logger.Trace("Updating group stats by group from AnimeSeriesRepository.Delete: {0}", entity.AnimeGroupID);
                Repo.AnimeGroup.BeginUpdate(entity.AnimeGroupID)?.Commit((true, true, true));
            }
        }

        internal override int SelectKey(SVR_AnimeSeries entity) => entity.AnimeSeriesID;

        internal override void PopulateIndexes()
        {

            AniDBIds = Cache.CreateIndex(a => a.AniDB_ID);
            Groups = Cache.CreateIndex(a => a.AnimeGroupID);
        }

        internal override void ClearIndexes()
        {
            Groups = null;
            AniDBIds = null;
        }

        public override void PreInit(IProgress<InitProgress> progress, int batchSize)
        {
            List<SVR_AnimeSeries> sers = Where(a => a.ContractVersion < SVR_AnimeSeries.CONTRACT_VERSION || a.Contract==null || a.Contract.AniDBAnime == null || a.Contract.AniDBAnime.AniDBAnime == null).ToList();
            if (sers.Count == 0)
                return;
            InitProgress regen = new InitProgress();
            regen.Title = string.Format(Commons.Properties.Resources.Database_Validating, typeof(AnimeEpisode_User).Name, " Regen");
            regen.Step = 0;
            regen.Total = sers.Count;
            BatchAction(sers, batchSize, (s, original) =>
            {
                regen.Step++;
                progress.Report(regen);
            }, (true, true, false, false));

            regen.Step = regen.Total;
            progress.Report(regen);
            Changes.AddOrUpdateRange(WhereAll().Select(a=> a.AnimeSeriesID));
        }

        public ChangeTracker<int> GetChangeTracker()
        {
            lock (Changes)
            {
                return Changes;
            }
        }
        /*
        //TODO DBRefactor
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
        */
        public SVR_AnimeSeries GetByAnimeID(int id)
        {
            lock (Cache)
            {
                return AniDBIds.GetOne(id);
            }
        }


        public List<SVR_AnimeSeries> GetByGroupID(int groupid)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Groups.GetMultiple(groupid);
                return Table.Where(a => a.AnimeGroupID==groupid).ToList();
            }
        }
        public List<int> GetSeriesIdByGroupID(int groupid)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Groups.GetMultiple(groupid).Select(a=>a.AnimeSeriesID).ToList();
                return Table.Where(a => a.AnimeGroupID == groupid).Select(a=>a.AnimeSeriesID).ToList();
            }
        }

        public List<SVR_AnimeSeries> GetWithMissingEpisodes()
        {
            return Where(a => a.MissingEpisodeCountGroups > 0).OrderByDescending(a => a.EpisodeAddedDate).ToList();
        }

        public List<SVR_AnimeSeries> GetMostRecentlyAdded(int maxResults)
        {
            //+15 ?
            return WhereAll().OrderByDescending(a => a.DateTimeCreated).Take(maxResults + 15).ToList();
        }
    }
}