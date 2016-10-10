using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NLog;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories.Cached
{
    public class AnimeGroupRepository : BaseCachedRepository<AnimeGroup, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PocoIndex<int, AnimeGroup, int> Parents;

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
                    AnimeGroup ngrp = GetByID(cr.AnimeGroupParentID.Value);
                    if (ngrp != null)
                        Save(ngrp, false, true);
                }
            };

        }

        public static AnimeGroupRepository Create()
        {
            return new AnimeGroupRepository();
        }

        protected override int SelectKey(AnimeGroup entity)
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
            List<AnimeGroup> grps = Cache.Values.Where(a => a.ContractVersion < AnimeGroup.CONTRACT_VERSION).ToList();
            int max = grps.Count;
            int cnt = 0;
            foreach (AnimeGroup g in grps)
            {
                Save(g, true, false, false);
                cnt++;
                if (cnt % 10 == 0)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, typeof(AnimeGroup).Name,
                        " DbRegen - " + cnt + "/" + max);
                }
            }
            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, typeof(AnimeGroup).Name,
                " DbRegen - " + max + "/" + max);
        }


        //Disable base saves.
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Save(AnimeGroup obj) { throw new NotSupportedException(); }
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Save(IReadOnlyCollection<AnimeGroup> objs) { throw new NotSupportedException(); }

        public void Save(AnimeGroup grp, bool updategrpcontractstats, bool recursive, bool verifylockedFilters = true)
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
                    RepoFactory.GroupFilter.CreateOrVerifyTagsAndYearsFilters(false, grp.Contract.Stat_AllTags,grp.Contract.Stat_AirDate_Min);
                    //This call will create extra years or tags if the Group have a new year or tag
                    grp.UpdateGroupFilters(types, null);
                }
                if (grp.AnimeGroupParentID.HasValue && recursive)
                {
                    AnimeGroup pgroup = GetByID(grp.AnimeGroupParentID.Value);
					// This will avoid the recursive error that would be possible, it won't update it, but that would be
					// the least of the issues
					if(pgroup != null && pgroup.AnimeGroupParentID == grp.AnimeGroupID)
						Save(pgroup, updategrpcontractstats, true, verifylockedFilters);
                }
            }
        }

        public void InsertBatch(ISessionWrapper session, IEnumerable<AnimeGroup> groups)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (groups == null)
                throw new ArgumentNullException(nameof(groups));

            foreach (AnimeGroup group in groups)
            {
                session.Insert(group);
                Changes.AddOrUpdate(group.AnimeGroupID);
            }
        }

        public void UpdateBatch(ISessionWrapper session, IEnumerable<AnimeGroup> groups)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (groups == null)
                throw new ArgumentNullException(nameof(groups));

            foreach (AnimeGroup group in groups)
            {
                session.Update(group);
                Changes.AddOrUpdate(group.AnimeGroupID);
            }
        }


        public List<AnimeGroup> GetByParentID(int parentid)
        {
            return Parents.GetMultiple(parentid);
        }






        public List<AnimeGroup> GetAllTopLevelGroups()
        {
            return Parents.GetMultiple(0);
        }

        public ChangeTracker<int> GetChangeTracker()
        {
            return Changes;
        }
    }
}