using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Cached
{
    public class AnimeGroupRepository : BaseCachedRepository<SVR_AnimeGroup, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PocoIndex<int, SVR_AnimeGroup, int> Parents;

        private ChangeTracker<int> Changes = new ChangeTracker<int>();

        private AnimeGroupRepository()
        {
            BeginDeleteCallback = (cr) =>
            {
                RepoFactory.AnimeGroup_User.Delete(RepoFactory.AnimeGroup_User.GetByGroupID(cr.AnimeGroupID));
                cr.DeleteFromFilters();
            };
            EndDeleteCallback = (cr) =>
            {
                if (cr.AnimeGroupParentID.HasValue && cr.AnimeGroupParentID.Value > 0)
                {
                    logger.Trace("Updating group stats by group from AnimeGroupRepository.Delete: {0}",
                        cr.AnimeGroupParentID.Value);
                    SVR_AnimeGroup ngrp = GetByID(cr.AnimeGroupParentID.Value);
                    if (ngrp != null)
                        Save(ngrp, false, true);
                }
            };
        }

        public static AnimeGroupRepository Create()
        {
            return new AnimeGroupRepository();
        }

        protected override int SelectKey(SVR_AnimeGroup entity)
        {
            return entity.AnimeGroupID;
        }

        public override void PopulateIndexes()
        {
            Changes.AddOrUpdateRange(Cache.Keys);
            Parents = Cache.CreateIndex(a => a.AnimeGroupParentID ?? 0);
        }

        public override void RegenerateDb()
        {
            List<SVR_AnimeGroup> grps = Cache.Values.Where(a => a.ContractVersion < SVR_AnimeGroup.CONTRACT_VERSION)
                .ToList();
            int max = grps.Count;
            int cnt = 0;
            ServerState.Instance.CurrentSetupStatus = string.Format(Commons.Properties.Resources.Database_Validating,
                typeof(AnimeGroup).Name, " DbRegen");
            if (max <= 0) return;
            foreach (SVR_AnimeGroup g in grps)
            {
                g.Description = g.Description?.Replace('`', '\'');
                g.GroupName = g.GroupName?.Replace('`', '\'');
                g.SortName = g.SortName?.Replace('`', '\'');
                Save(g, true, false, false);
                cnt++;
                if (cnt % 10 == 0)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(
                        Commons.Properties.Resources.Database_Validating, typeof(AnimeGroup).Name,
                        " DbRegen - " + cnt + "/" + max);
                }
            }
            ServerState.Instance.CurrentSetupStatus = string.Format(Commons.Properties.Resources.Database_Validating,
                typeof(AnimeGroup).Name,
                " DbRegen - " + max + "/" + max);
        }

        public override void Save(SVR_AnimeGroup obj)
        {
            Save(obj, true, true);
        }

        public void Save(SVR_AnimeGroup grp, bool updategrpcontractstats, bool recursive,
            bool verifylockedFilters = true)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                ISessionWrapper sessionWrapper = session.Wrap();
                lock (globalDBLock)
                {
                    lock (grp)
                    {
                        if (grp.AnimeGroupID == 0)
                            //We are creating one, and we need the AnimeGroupID before Update the contracts
                        {
                            grp.Contract = null;
                            using (var transaction = session.BeginTransaction())
                            {
                                session.SaveOrUpdate(grp);
                                transaction.Commit();
                            }
                        }
                        var types = grp.UpdateContract(sessionWrapper, updategrpcontractstats);
                        //Types will contains the affected GroupFilterConditionTypes
                        using (var transaction = session.BeginTransaction())
                        {
                            SaveWithOpenTransaction(session, grp);
                            transaction.Commit();
                        }
                        lock (Changes)
                        {
                            Changes.AddOrUpdate(grp.AnimeGroupID);
                        }

                        if (verifylockedFilters)
                        {
                            RepoFactory.GroupFilter.CreateOrVerifyDirectoryFilters(false, grp.Contract.Stat_AllTags,
                                grp.Contract.Stat_AllYears, grp.Contract.Stat_AllSeasons);
                            //This call will create extra years or tags if the Group have a new year or tag
                            grp.UpdateGroupFilters(types, null);
                        }
                    }
                }
                if (grp.AnimeGroupParentID.HasValue && recursive)
                {
                    SVR_AnimeGroup pgroup = GetByID(grp.AnimeGroupParentID.Value);
                    // This will avoid the recursive error that would be possible, it won't update it, but that would be
                    // the least of the issues
                    if (pgroup != null && pgroup.AnimeGroupParentID == grp.AnimeGroupID)
                        Save(pgroup, updategrpcontractstats, true, verifylockedFilters);
                }
            }
        }

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

        public List<SVR_AnimeGroup> GetByParentID(int parentid)
        {
            lock (Cache)
            {
                return Parents.GetMultiple(parentid);
            }
        }

        public List<SVR_AnimeGroup> GetAllTopLevelGroups()
        {
            lock (Cache)
            {
                return Parents.GetMultiple(0);
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