using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Shoko.Commons.Properties;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;
using Shoko.Server.Repositories.Cache;

namespace Shoko.Server.Repositories.Repos
{
    public class AnimeGroupRepository : BaseRepository<SVR_AnimeGroup, int, (bool updategrpcontractstats, bool recursive, bool verifylockedFilters)>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PocoIndex<int, SVR_AnimeGroup, int> Parents;

        private readonly ChangeTracker<int> Changes = new ChangeTracker<int>();

        internal override object BeginSave(SVR_AnimeGroup entity, SVR_AnimeGroup original_entity,
            (bool updategrpcontractstats, bool recursive, bool verifylockedFilters) parameters)
        {
            return entity.UpdateContract(parameters.updategrpcontractstats);
        }

        internal override void EndSave(SVR_AnimeGroup entity, object returnFromBeginSave,
            (bool updategrpcontractstats, bool recursive, bool verifylockedFilters) parameters)
        {
            HashSet<GroupFilterConditionType> types = (HashSet<GroupFilterConditionType>)returnFromBeginSave;
            lock (Changes)
            {
                Changes.AddOrUpdate(entity.AnimeGroupID);
            }

            if (parameters.verifylockedFilters)
            {
                Repo.Instance.GroupFilter.CreateOrVerifyDirectoryFilters(null, false, entity.Contract.Stat_AllTags,
                    entity.Contract.Stat_AllYears, entity.Contract.Stat_AllSeasons);
                //This call will create extra years or tags if the Group have a new year or tag
                entity.UpdateGroupFilters(types);
            }
            if (entity.AnimeGroupParentID.HasValue && parameters.recursive && entity.AnimeGroupParentID.Value!=entity.AnimeGroupID)
            {
                using (var upd = Repo.Instance.AnimeGroup.BeginAddOrUpdate(() => GetByID(entity.AnimeGroupParentID.Value)))
                {
                    if (upd.IsUpdate)
                        upd.Commit((parameters.updategrpcontractstats, true, parameters.verifylockedFilters)); // WARNING: This might creata a recursion loop
                }
            }
        }

        internal override object BeginDelete(SVR_AnimeGroup entity, (bool updategrpcontractstats, bool recursive, bool verifylockedFilters) parameters)
        {
            Repo.Instance.AnimeGroup_User.FindAndDelete(()=>Repo.Instance.AnimeGroup_User.GetByGroupID(entity.AnimeGroupID));
            entity.DeleteFromFilters();
            return null;
        }

        internal override void EndDelete(SVR_AnimeGroup entity, object returnFromBeginDelete,
            (bool updategrpcontractstats, bool recursive, bool verifylockedFilters) parameters)
        {
            if (entity.AnimeGroupParentID.HasValue && entity.AnimeGroupParentID.Value > 0 && entity.AnimeGroupParentID!=entity.AnimeGroupID)
            {
                logger.Trace("Updating group stats by group from AnimeGroupRepository.Delete: {0}", entity.AnimeGroupParentID.Value);
                Repo.Instance.AnimeGroup.Touch(entity.AnimeGroupParentID.Value,(false, true, true));
            }
        }

        internal override int SelectKey(SVR_AnimeGroup entity) => entity.AnimeGroupID;

        internal override void PopulateIndexes()
        {
            Parents = Cache.CreateIndex(a => a.AnimeGroupParentID ?? 0);
        }

        internal override void ClearIndexes()
        {
            Parents = null;
        }

        public override void PreInit(IProgress<InitProgress> progress, int batchSize)
        {
            List<SVR_AnimeGroup> grps = Where(a => a.ContractVersion < SVR_AnimeGroup.CONTRACT_VERSION).ToList();
            if (grps.Count == 0)
                return;
            InitProgress regen = new InitProgress();
            regen.Title = String.Format(Resources.Database_Validating, typeof(AnimeEpisode_User).Name, " Regen");
            regen.Step = 0;
            regen.Total = grps.Count;
            BatchAction(grps, batchSize, (g, original) =>
            {
                g.Description = g.Description?.Replace('`', '\'');
                g.GroupName = g.GroupName?.Replace('`', '\'');
                g.SortName = g.SortName?.Replace('`', '\'');
            }, (true, false, false));
            
            regen.Step = regen.Total;
            progress.Report(regen);

        }

        public override void PostInit(IProgress<InitProgress> progress, int batchSize)
        {
            lock (Changes)
            {
                Changes.AddOrUpdateRange(WhereAll().Select(a => a.AnimeGroupID));
            }
        }
        /*
        //TODO DBRefactor

        public void InsertBatch(ISessionWrapper session, IReadOnlyCollection<SVR_AnimeGroup> groups)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (groups == null)
                throw new ArgumentNullException(nameof(groups));

            foreach (SVR_AnimeGroup group in groups)
            {
                lock (globalDBLock)
                {
                    lock (group)
                    {
                        session.Insert(group);
                        lock (Cache)
                        {
                            Cache.Update(group);
                        }
                    }
                }
            }

            lock (Changes)
            {
                Changes.AddOrUpdateRange(groups.Select(g => g.AnimeGroupID));
            }
        }

        public void UpdateBatch(ISessionWrapper session, IReadOnlyCollection<SVR_AnimeGroup> groups)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (groups == null)
                throw new ArgumentNullException(nameof(groups));

            foreach (SVR_AnimeGroup group in groups)
            {
                lock (globalDBLock)
                {
                    lock (group)
                    {
                        session.Update(group);
                        lock (Cache)
                        {
                            Cache.Update(group);
                        }
                    }
                }
            }

            lock (Changes)
            {
                Changes.AddOrUpdateRange(groups.Select(g => g.AnimeGroupID));
            }
        }

        /// <summary>
        /// Deletes all AnimeGroup records.
        /// </summary>
        /// <remarks>
        /// This method also makes sure that the cache is cleared.
        /// </remarks>
        /// <param name="session">The NHibernate session.</param>
        /// <param name="excludeGroupId">The ID of the AnimeGroup to exclude from deletion.</param>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is <c>null</c>.</exception>
        public void DeleteAll(ISessionWrapper session, int? excludeGroupId = null)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            // First, get all of the current groups so that we can inform the change tracker that they have been removed later
            var allGrps = GetAll();

            lock (globalDBLock)
            {
                // Then, actually delete the AnimeGroups
                if (excludeGroupId != null)
                {
                    session.CreateQuery("delete SVR_AnimeGroup ag where ag.id <> :excludeId")
                        .SetInt32("excludeId", excludeGroupId.Value)
                        .ExecuteUpdate();

                    lock (Changes)
                    {
                        Changes.RemoveRange(allGrps.Select(g => g.AnimeGroupID)
                            .Where(id => id != excludeGroupId.Value));
                    }
                }
                else
                {
                    session.CreateQuery("delete SVR_AnimeGroup ag")
                        .ExecuteUpdate();

                    lock (Changes)
                    {
                        Changes.RemoveRange(allGrps.Select(g => g.AnimeGroupID));
                    }
                }
            }

            // Finally, we need to clear the cache so that it is in sync with the database
            ClearCache();

            // If we're exlcuding a group from deletion, and it was in the cache originally, then re-add it back in
            if (excludeGroupId != null)
            {
                SVR_AnimeGroup excludedGroup = allGrps.FirstOrDefault(g => g.AnimeGroupID == excludeGroupId.Value);

                if (excludedGroup != null)
                {
                    lock (Cache)
                    {
                        Cache.Update(excludedGroup);
                    }
                }
            }
        }
        */
        public List<SVR_AnimeGroup> GetByParentID(int parentid)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Parents.GetMultiple(parentid);
                return Table.Where(a => a.AnimeGroupParentID==parentid).ToList();
            }
        }

        public List<SVR_AnimeGroup> GetAllTopLevelGroups()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Parents.GetMultiple(0);
                return Table.Where(a => !a.AnimeGroupParentID.HasValue || a.AnimeGroupParentID.Value == 0).ToList();
            }
        }
        public void KillEmAllExceptGrimorieOfZero()
        {
            using (RepoLock.ReaderLock())
            {

                List<SVR_AnimeGroup> grps;
                if (IsCached)
                {
                    grps = Cache.Values.Where(a => a.AnimeGroupID != 0).ToList();
                    Cache = null;
                    ClearIndexes();
                }
                else
                {
                    grps = Table.Where(a => a.AnimeGroupID != 0).ToList();
                }
                ShokoContext ctx = Provider.GetContext();
                ctx.AttachRange(grps);
                ctx.RemoveRange(grps);
                ctx.SaveChanges();
            }
        }
        public ChangeTracker<int> GetChangeTracker()
        {
            lock (Changes)
            {
                return Changes;
            }
        }

     
    }
}