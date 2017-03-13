using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Shoko.Models.Server;
using NHibernate;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Models;
using Shoko.Models.Enums;
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
            ServerState.Instance.CurrentSetupStatus = string.Format(Shoko.Commons.Properties.Resources.Database_Cache,
                typeof(AnimeGroup).Name, " DbRegen");
            if (max <= 0) return;
            foreach (SVR_AnimeGroup g in grps)
            {
                Save(g, true, false, false);
                cnt++;
                if (cnt % 10 == 0)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(
                        Shoko.Commons.Properties.Resources.Database_Cache, typeof(AnimeGroup).Name,
                        " DbRegen - " + cnt + "/" + max);
                }
            }
            ServerState.Instance.CurrentSetupStatus = string.Format(Shoko.Commons.Properties.Resources.Database_Cache,
                typeof(AnimeGroup).Name,
                " DbRegen - " + max + "/" + max);
        }


        //Disable base saves.
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Save(SVR_AnimeGroup obj)
        {
            throw new NotSupportedException();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Save(IReadOnlyCollection<SVR_AnimeGroup> objs)
        {
            throw new NotSupportedException();
        }

        public void Save(SVR_AnimeGroup grp, bool updategrpcontractstats, bool recursive,
            bool verifylockedFilters = true)
        {
            using (var session = DatabaseFactory.SessionFactory.OpenSession())
            {
                ISessionWrapper sessionWrapper = session.Wrap();
                HashSet<GroupFilterConditionType> types;
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
                    types = grp.UpdateContract(sessionWrapper, updategrpcontractstats);
                    //Types will contains the affected GroupFilterConditionTypes
                    using (var transaction = session.BeginTransaction())
                    {
                        base.SaveWithOpenTransaction(session, grp);
                        transaction.Commit();
                    }
                    Changes.AddOrUpdate(grp.AnimeGroupID);
                }
                if (verifylockedFilters)
                {
                    RepoFactory.GroupFilter.CreateOrVerifyTagsAndYearsFilters(false, grp.Contract.Stat_AllTags,
                        grp.Contract.Stat_AllYears);
                    //This call will create extra years or tags if the Group have a new year or tag
                    grp.UpdateGroupFilters(types, null);
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
                session.Insert(group);
            }

            Changes.AddOrUpdateRange(groups.Select(g => g.AnimeGroupID));
        }

        public void UpdateBatch(ISessionWrapper session, IReadOnlyCollection<SVR_AnimeGroup> groups)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (groups == null)
                throw new ArgumentNullException(nameof(groups));

            foreach (SVR_AnimeGroup group in groups)
            {
                session.Update(group);
            }

            Changes.AddOrUpdateRange(groups.Select(g => g.AnimeGroupID));
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

            // Then, actually delete the AnimeGroups
            if (excludeGroupId != null)
            {
                session.CreateQuery("delete AnimeGroup ag where ag.id <> :excludeId")
                    .SetInt32("excludeId", excludeGroupId.Value)
                    .ExecuteUpdate();

                Changes.RemoveRange(allGrps.Select(g => g.AnimeGroupID).Where(id => id != excludeGroupId.Value));
            }
            else
            {
                session.CreateQuery("delete AnimeGroup ag")
                    .ExecuteUpdate();

                Changes.RemoveRange(allGrps.Select(g => g.AnimeGroupID));
            }

            // Finally, we need to clear the cache so that it is in sync with the database
            ClearCache();

            // If we're exlcuding a group from deletion, and it was in the cache originally, then re-add it back in
            if (excludeGroupId != null)
            {
                SVR_AnimeGroup excludedGroup = allGrps.FirstOrDefault(g => g.AnimeGroupID == excludeGroupId.Value);

                if (excludedGroup != null)
                {
                    Cache.Update(excludedGroup);
                }
            }
        }

        public List<SVR_AnimeGroup> GetByParentID(int parentid)
        {
            return Parents.GetMultiple(parentid);
        }

        public List<SVR_AnimeGroup> GetAllTopLevelGroups()
        {
            return Parents.GetMultiple(0);
        }

        public ChangeTracker<int> GetChangeTracker()
        {
            return Changes;
        }
    }
}